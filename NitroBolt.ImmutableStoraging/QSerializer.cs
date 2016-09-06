using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NitroBolt.Functional;
using NitroBolt.QSharp;

namespace NitroBolt.ImmutableStoraging
{
    public class PathEntry
    {
        public PathEntry(Type type, string property)
        {
            this.Type = type;
            this.Property = property;
        }

        public readonly Type Type;
        public readonly string Property;
    }

    public class QSerializer
    {
        public QSerializer(Dictionary<Type, string> ids, Dictionary<Type, Dictionary<string, string>> references,
            Dictionary<Type, Dictionary<string, PushInfo[]>> pushes)
        {
            this.IdPropertyByType = ids;
            this.Type_References = references;
            this.Pushes = pushes;
        }

        public readonly Dictionary<Type, Dictionary<string, PushInfo[]>> Pushes;

        public readonly Dictionary<Type, string> IdPropertyByType;

        public readonly Dictionary<Type, Dictionary<string, string>> Type_References;

        static readonly Dictionary<Type, ImmutablePropertyInfo[]> PropertyIndex =
            new Dictionary<Type, ImmutablePropertyInfo[]>();

        static readonly object PropertyIndex_Locker = new object();

        public static ImmutablePropertyInfo[] Properties(Type type)
        {
            lock (PropertyIndex_Locker)
            {
                var properties = PropertyIndex.Find(type);
                if (properties == null)
                {
                    properties = Sync(Childs(type)).ToArray();
                    PropertyIndex[type] = properties;
                }
                return properties;
            }
        }

        public object Value(object item, bool isRef = false)
        {
            if (item == null)
                return null;
            var type = item.GetType();
            if (IsPrimitiveType(type))
                return item;

            var childs = Properties(type);
            var idProperty = childs.FirstOrDefault(child => child.Name == IdPropertyByType.Find(type));
            var refProperties = Type_References.Find(type);

            return SaveId(item, idProperty);
            //return new QSharp.QNode(,
            //  isRef
            //  ? Array<QSharp.QNode>.Empty
            //  : childs
            //    .Where(child => child != idProperty)
            //    .Select(prop =>
            //    {
            //      var value = SaveProp(item, prop, refProperties.Find(prop.Name) != null);
            //      return value != null && value.Any() ? new QSharp.QNode(prop.Name, value) : null;
            //    })
            //    .Where(value => value != null)
            //    .ToArray()
            //);
        }

        public class SerializeTypeInfo
        {
            public SerializeTypeInfo(ImmutablePropertyInfo idProperty, ImmutablePropertyInfo[] properties,
                Dictionary<string, string> refProperties,
                Dictionary<string, PushInfo[]> pushes,
                System.Reflection.ConstructorInfo constructor)
            {
                this.IdProperty = idProperty;
                this.Properties = properties;
                this.Properties_Wo_Id = properties.Where(property => property != idProperty).ToArray();
                this.RefProperties = refProperties;
                this.Pushes = pushes;
                this.Constructor = constructor;

                if (constructor != null)
                {
                    var parameters = constructor.GetParameters();

                    this.Parameters = parameters
                        .Select(parameter =>
                            {
                                var pName = ToUpper(parameter.Name);
                                return new SerializeParameterInfo(pName, parameter.ParameterType,
                                    IdProperty?.Name == pName, refProperties.Find(pName), pushes.Find(pName));
                            }
                        )
                        .ToArray();
                    this.ParameterIndexByName =
                        this.Parameters.Select((p, i) => new {p.Name, i})
                            .ToDictionary(pair => pair.Name, pair => pair.i);

                    this.Construct = constructor.ToFunc(parameters);
                }
                //if (idProperty != null && idProperty.Field != null && idProperty.Field.DeclaringType == typeof(Planet))
                //{
                //  Construct = values => new Planet((long)values[0], (int)values[1], (int)values[2], (int)values[3], (int)values[4], (int)values[5], (int)values[6]);
                //}
            }

            public readonly ImmutablePropertyInfo IdProperty;
            public readonly ImmutablePropertyInfo[] Properties;
            public readonly ImmutablePropertyInfo[] Properties_Wo_Id;
            public readonly Dictionary<string, string> RefProperties;
            public readonly Dictionary<string, PushInfo[]> Pushes;
            public readonly System.Reflection.ConstructorInfo Constructor;
            public readonly Func<object[], object> Construct;
            //public readonly System.Reflection.ParameterInfo[] Parameters;
            public readonly SerializeParameterInfo[] Parameters;
            public readonly Dictionary<string, int> ParameterIndexByName;
        }

        public class SerializeParameterInfo
        {
            public SerializeParameterInfo(string name, Type parameterType, bool isId, string refProperty,
                PushInfo[] push)
            {
                this.Name = name;
                this.ParameterType = parameterType;
                this.IsId = isId;
                this.RefProperty = refProperty;
                this.Push = push;
            }

            public readonly string Name;
            public readonly Type ParameterType;
            public readonly bool IsId;
            public readonly string RefProperty;
            public readonly PushInfo[] Push;

            public override string ToString()
            {
                return $"{(IsId ? "*" : "")}{Name} {ParameterType.Name}";
            }
        }

        Dictionary<Type, SerializeTypeInfo> SerializeTypeInfos = new Dictionary<Type, SerializeTypeInfo>();

        public SerializeTypeInfo SerializeInfoByType(Type type)
        {
            var info = SerializeTypeInfos.Find(type);
            if (info == null)
            {
                var childs = Properties(type);
                var idProperty = childs.FirstOrDefault(child => child.Name == IdPropertyByType.Find(type));
                var refProperties = Type_References.Find(type);
                var pushes = Pushes.Find(type);

                var constructor = type.GetConstructors().FirstOrDefault();

                info = new SerializeTypeInfo(idProperty, childs, refProperties, pushes, constructor);
                SerializeTypeInfos[type] = info;
            }
            return info;
        }

        public class Loader
        {
            public Loader(Type type, Func<Type, bool> isTypeLoading,
                Func<Type, QSharp.QNode, Dictionary<string, Dictionary<object, object>>, object> load)
            {
                this.Type = type;
                this.IsTypeLoading = isTypeLoading;
                this.Load = load;
            }

            public readonly Type Type;
            public readonly Func<Type, bool> IsTypeLoading;
            public readonly Func<Type, QSharp.QNode, Dictionary<string, Dictionary<object, object>>, object> Load;
        }

        public static readonly Loader[] Loaders = new[]
        {
            //new Loader(null, type => IsNullableType(type), (type, q, data)=>Load(type.GetGenericArguments().First(), q, data)),
            new Loader(typeof(double), null, (type, q, data) => ConvertHlp.ToDouble(q.Value)),
            //new Loader(typeof(int), null, (type, q, data) => ConvertHlp.ToInt(q._Value)),
            new Loader(typeof(int), null, (type, q, data) => Converter.ToInt(q.Value?.ToString())),
            new Loader(typeof(long), null, (type, q, data) => Converter.ToLong(q.Value?.ToString())),
            new Loader(typeof(bool), null, (type, q, data) => bool.Parse(q.Value?.ToString())),
            new Loader(typeof(Guid), null, (type, q, data) => ConvertHlp.ToGuid(q.Value)),
            new Loader(typeof(string), null, (type, q, data) => q.Value?.ToString()),
            new Loader(typeof(DateTime), null, (type, q, data) => DateTime.Parse(q.Value?.ToString(), Ru)),

            new Loader(null, type => type.IsEnum, (type, q, data) => Enum.Parse(type, q.Value?.ToString())),
        };

        public readonly
            Dictionary<Type, Func<Type, QSharp.QNode, Dictionary<string, Dictionary<object, object>>, object>>
            LoaderIndex =
                Loaders.Where(loader => loader.Type != null)
                    .ToDictionary(loader => loader.Type, loader => loader.Load);

        public static readonly Loader[] UniLoaders = Loaders.Where(loader => loader.IsTypeLoading != null).ToArray();

        public Func<Type, QSharp.QNode, Dictionary<string, Dictionary<object, object>>, object> GetLoader(Type type)
        {
            var loader = LoaderIndex.Find(type);
            if (loader == null)
            {
                if (IsNullableType(type))
                {
                    var sourceType = type.GetGenericArguments().First();
                    var sourceLoader = GetLoader(sourceType);
                    loader = (_type, _q, _data) => sourceLoader(sourceType, _q, _data);
                }
                else
                {
                    var uniLoader = UniLoaders.FirstOrDefault(_uniLoader => _uniLoader.IsTypeLoading(type));
                    if (uniLoader != null)
                        loader = uniLoader.Load;
                    else
                        loader = LoadStructure;
                }
                LoaderIndex[type] = loader;
            }
            return loader;
        }

        public object Load(Type type, QSharp.QNode q, Dictionary<string, Dictionary<object, object>> data)
        {
            if (q == null)
                return null;
            return GetLoader(type)(type, q, data);
        }


        public object LoadStructure(Type type, QSharp.QNode q, Dictionary<string, Dictionary<object, object>> data)
        {
            var typeInfo = SerializeInfoByType(type);
            if (typeInfo == null || typeInfo.Constructor == null)
                return null;

            var sourceValues = new QNode[typeInfo.Parameters.Length][];
            if (q.Nodes != null)
            {
                var i = -1;
                foreach (var node in q.Nodes)
                {
                    var name = node.Value?.ToString();
                    i++;
                    if (i >= typeInfo.Parameters.Length || typeInfo.Parameters[i].Name != name)
                    {
                        var _i = typeInfo.ParameterIndexByName.FindValue(name);
                        if (_i == null)
                            continue;
                        i = _i.Value;
                    }
                    sourceValues[i] = ArrayConcat(sourceValues[i], node.Nodes.ToArray());
                }
            }


            var values = new object[typeInfo.Parameters.Length];

            for (var i = 0; i < typeInfo.Parameters.Length; ++i)
            {
                var p = typeInfo.Parameters[i];

                var qs = p.IsId ? new[] {new QSharp.QNode(q.Value)} : sourceValues[i];
                var value = p.RefProperty != null
                    ? Load_Ref(p.RefProperty, p.ParameterType, qs, data)
                    : Load_s(p.ParameterType, qs, data);

                if (value == null && p.ParameterType == typeof(bool))
                    value = false;


                values[i] = value;

                var prop_pushes = typeInfo.Pushes.Find(p.Name);
                if (prop_pushes == null)
                    continue;

                foreach (var push in prop_pushes)
                {
                    data = new Dictionary<string, Dictionary<object, object>>(data);
                    var elementType = push.ElementType ?? Collection_ElementType(p.ParameterType);
                    var elementTypeInfo = SerializeInfoByType(elementType);

                    //var elementIdType = IdPropertyByType.Find(elementType);
                    //var elementIdProperty = Properties(elementType).FirstOrDefault(child => child.Name == elementIdType);
                    var val = push.Converter != null ? push.Converter(value) : value;
                    data[push.Name] =
                        Else_Empty(val.As<System.Collections.IEnumerable>())
                            .OfType<object>()
                            .ToDictionary(obj => GetId(obj, elementTypeInfo.IdProperty));
                }
            }

            return typeInfo.Construct != null ? typeInfo.Construct(values) : typeInfo.Constructor.Invoke(values);
        }

        public static T[] ArrayConcat<T>(T[] array1, T[] array2)
        {
            if (array1 == null || array1.Length == 0)
                return array2;
            if (array2 == null || array2.Length == 0)
                return array1;
            var array = new T[array1.Length + array2.Length];
            Array.Copy(array1, array, array1.Length);
            Array.Copy(array2, 0, array, array1.Length, array2.Length);
            return array;
        }

        private object Load_Ref(string targetRef, Type type, QSharp.QNode[] qs,
            Dictionary<string, Dictionary<object, object>> data)
        {
            if (IsCollectionType(type))
            {
                if (qs == null)
                    return null;
                var element_type = Collection_ElementType(type);
                var array = Array.CreateInstance(element_type, qs.Length);
                for (var i = 0; i < array.Length; ++i)
                    array.SetValue(Load_Ref(targetRef, element_type, new[] {qs[i]}, data), i);
                return array;
            }

            var typeInfo = SerializeInfoByType(type);
            //var childs = Properties(type);
            //var idPropertyName = IdPropertyByType.Find(type);
            //var idProperty = childs.FirstOrDefault(child => child.Name == idPropertyName);

            return data.Find(targetRef).Find(Load_s(typeInfo.IdProperty.Type, qs, data));
        }

        object Load_s(Type type, QSharp.QNode[] qs, Dictionary<string, Dictionary<object, object>> data)
        {
            if (IsCollectionType(type))
            {
                if (qs == null)
                    return null;
                var element_type = Collection_ElementType(type);
                var array = Array.CreateInstance(element_type, qs.Length);
                for (var i = 0; i < array.Length; ++i)
                    array.SetValue(Load(element_type, qs[i], data), i);
                if (type.IsGenericType)
                {
                    var create = typeof(System.Collections.Immutable.ImmutableArray).GetMethods()
                        .Where(method => method.Name == "Create")
                        .FirstOrDefault(method =>
                            {
                                var parameters = method.GetParameters();
                                return parameters.Length == 1 && parameters[0].ParameterType.IsArray;
                            }
                        );
                    return create.MakeGenericMethod(element_type).Invoke(null, new object[] {array});
                }
                return array;
            }
            return Load(type, qs.FirstOrDefault(), data);
        }


        public QSharp.QNode Save(object item, bool isRef = false, Type itemType = null)
        {
            if (item == null)
                return null;
            var type = itemType ?? item.GetType();
            if (IsPrimitiveType(type))
                return new QSharp.QNode(ValueToText(item));

            var typeInfo = SerializeInfoByType(type);

            var id = SaveId(item, typeInfo.IdProperty);
            if (isRef)
                return new QNode(id);
            else
            {
                var nodes = new List<QNode>(typeInfo.Properties_Wo_Id.Length);
                foreach (var prop in typeInfo.Properties_Wo_Id)
                {
                    var value = SaveProp(item, prop, typeInfo.RefProperties.Find(prop.Name) != null);
                    if (value != null && value.Any())
                        nodes.Add(new QSharp.QNode(prop.Name, value));
                }
                return new QNode(id, nodes.ToArray());
            }
        }

        QSharp.QNode[] SaveProp(object item, ImmutablePropertyInfo prop, bool isRef = false)
        {
            var child = prop.Value(item);
            if (IsCollectionType(prop.Type))
            {
                if (child == null)
                    return Array<QSharp.QNode>.Empty;
                var elementType = Collection_ElementType(prop.Type);
                if (elementType == typeof(object))
                    elementType = null;
                return child.As<System.Collections.IEnumerable>()
                    .Cast<object>()
                    .Select(element => Save(element, isRef, itemType: elementType))
                    .ToArray();
            }
            else
                return child != null
                    ? new[] {Save(child, isRef, itemType: prop.ReducedType != typeof(object) ? prop.ReducedType : null)}
                    : Array<QSharp.QNode>.Empty;
        }


        static readonly System.Globalization.CultureInfo Ru = System.Globalization.CultureInfo.GetCultureInfo("ru-Ru");
        static readonly System.Globalization.CultureInfo En = System.Globalization.CultureInfo.InvariantCulture;

        static string ValueToText(object value)
        {
            if (value is float)
                return ((float) value).ToString(En);
            if (value is double)
                return ((double) value).ToString(En);
            if (value is DateTime)
                return ((DateTime) value).ToString(Ru);
            return value.ToString();
        }

        //static string ToText(int value)
        //{
        //  if (value == 0)
        //    return "0";
        //  if (value == int.MinValue)
        //    return int.MinValue.ToString();
        //  var isNegative = false;
        //  if (value < 0)
        //  {
        //    isNegative = true;
        //    value = -value;
        //  }
        //  var chars = new List<char>();
        //  for (;value > 0; )
        //  {
        //    chars.Add((char)('0' + (value % 10)));
        //    value /= 10;
        //  }
        //  if (isNegative)
        //    chars.Add('-');
        //  chars.Reverse();
        //  return new string(chars.ToArray());
        //}

        public object GetId(object item, ImmutablePropertyInfo idProperty)
        {
            if (idProperty == null)
                return null;
            //var value = MetaTech.Library.ReflectionExtension.ReflectionHelper._P<object>(item, idProperty.Name);
            var value = idProperty.Value(item);
            if (value == null)
                return null;
            if (IsPrimitiveType(idProperty.Type))
                return value;

            var type = idProperty.Type;
            var typeInfo = SerializeInfoByType(type);

            return GetId(value, typeInfo.IdProperty);
        }

        public object SaveId(object item, ImmutablePropertyInfo idProperty)
        {
            return GetId(item, idProperty) ?? "q";
        }


        public static IEnumerable<ImmutablePropertyInfo> Sync(IEnumerable<ImmutablePropertyInfo> properties)
        {
            return properties.GroupBy(property => property.Name.OrDefault(property.InitialName).ToLower())
                .Select(group => new ImmutablePropertyInfo
                    (
                        group.Select(p => p.Field).FirstOrDefault(field => field != null),
                        group.Select(p => p.Property).FirstOrDefault(property => property != null),
                        group.Select(p => p.InitialName).FirstOrDefault(name => name != null),
                        group.First().Type,
                        group.First().ReducedType
                    )
                )
                .Where(prop => prop.Name != null && prop.InitialName != null)
                .ToArray();
        }

        public IEnumerable<Type> Browse(Type root, int maxLevel = 5)
        {
            var types = new Dictionary<Type, int>();
            var sequences = new Queue<Type>();
            sequences.Enqueue(root);
            types[root] = 0;

            for (; sequences.Any();)
            {
                var type = sequences.Dequeue();
                var level = types.FindValue(type).OrDefault(0);
                if (level >= maxLevel)
                    continue;

                var refs = Type_References.Find(type);

                foreach (var property in Sync(Childs(type)))
                {
                    var child = property.ReducedType;

                    if (types.FindValue(child) != null)
                        continue;
                    if (IsPrimitiveType(child))
                        continue;
                    if (refs.Find(property.Name) != null)
                        continue;

                    sequences.Enqueue(child);
                    types[child] = level + 1;
                }
            }
            return types.Keys.ToArray();
        }

        public static IEnumerable<ImmutablePropertyInfo> Childs(Type type, bool isOnlyProperties = false)
        {
            if (IsPrimitiveType(type))
                yield break;

            var childs = type.GetFields()
                .Where(field => !field.IsStatic)
                .Select(field => new ImmutablePropertyInfo(field, null, null, field.FieldType, null))
                .Concat(
                    type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                        .Select(property => new ImmutablePropertyInfo(null, property, null, property.PropertyType, null)));
            if (!isOnlyProperties)
                childs =
                    childs.Concat(
                        type.GetConstructors()
                            .SelectMany(
                                constructor =>
                                    constructor.GetParameters()
                                        .Select(
                                            parameter =>
                                                new ImmutablePropertyInfo(null, null, parameter.Name,
                                                    parameter.ParameterType, null))));

            foreach (var child in childs)
            {
                yield return
                    new ImmutablePropertyInfo(child.Field, child.Property, child.InitialName, child.Type,
                        ReduceType(child.Type));
            }
        }

        public static Type ReduceType(Type type)
        {
            for (;;)
            {
                if (IsPrimitiveType(type))
                    return type;
                if (IsCollectionType(type))
                {
                    type = Collection_ElementType(type);
                    continue;
                }
                if (IsNullableType(type))
                {
                    type = type.GetGenericArguments().First();
                    continue;
                }

                return type;
            }
        }

        public static Type ReduceNullable(Type type)
        {
            if (IsNullableType(type))
                return type.GetGenericArguments().First();
            return type;
        }

        public static bool IsNullableType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static bool IsPrimitiveType(Type type)
        {
            if (PrimitiveTypes.Contains(type)
                || type.IsEnum)
                return true;
            var fullName = type.FullName;
            if (fullName.Length < 8 || fullName[7] != 'F' && fullName[7] != 'A')
                return false;
            return fullName.StartsWith("System.Func`") || type.FullName.StartsWith("System.Action`");
        }

        public static bool IsCollectionType(Type type)
        {
            type = ReduceNullable(type);
            if (type.IsArray)
                return true;
            return type.IsGenericType &&
                   type.GetGenericTypeDefinition() == typeof(System.Collections.Immutable.ImmutableArray<>);
        }

        public static Type Collection_ElementType(Type type)
        {
            type = ReduceNullable(type);
            if (type.IsArray)
                return type.GetElementType();
            if (type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(System.Collections.Immutable.ImmutableArray<>))
                return type.GetGenericArguments()[0];
            return null;
        }

        static readonly HashSet<Type> PrimitiveTypes = new HashSet<Type>(new[]
        {
            typeof(bool),
            typeof(int),
            typeof(long),
            typeof(double),
            typeof(string),
            typeof(Guid),
            typeof(DateTime),
            typeof(System.Drawing.Point),
            typeof(Action),
        });


        public static string TypeInitials(Type type)
        {
            var childs = Childs(type, isOnlyProperties: true).ToArray();
            return string.Format(
                @"
public {0} (
{1}
)
{{
{2}
}}
",
                type.Name,
                childs.Select(child =>
                                string.Format("  {0} {1} = null", ToText(child.Type), ToLower(child.Name))
                    )
                    .JoinToString(",\r\n"),
                childs.Select(child =>
                                string.Format("  this.{0} = {1};", child.Name, ToLower(child.Name))
                    )
                    .JoinToString("\r\n")
            );
        }

        static string ToLower(string text)
        {
            if (text == null)
                return null;
            return text.Substring(0, 1).ToLower() + text.Substring(1);
        }

        public static string ToUpper(string text)
        {
            if (text == null)
                return null;
            return text.Substring(0, 1).ToUpper() + text.Substring(1);
        }

        static string ToText(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return string.Format("{0}?", ToText(type.GetGenericArguments().First()));
            }
            return type.Name;
        }


        static System.Collections.IEnumerable Else_Empty(System.Collections.IEnumerable items)
        {
            if (items != null)
                return items;
            return new object[] {};
        }
    }

    public class PushInfo
    {
        public PushInfo(string name, Type elementType = null, Func<object, object> converter = null)
        {
            this.Name = name;
            this.ElementType = elementType;
            this.Converter = converter;
        }

        public readonly string Name;
        public readonly Type ElementType;
        public readonly Func<object, object> Converter;
    }


    public class ImmutablePropertyInfo
    {
        public ImmutablePropertyInfo(System.Reflection.FieldInfo field, System.Reflection.PropertyInfo property,
            string initialName, Type type, Type reducedType)
        {
            this.Field = field;
            this.Property = property;
            this.InitialName = initialName;
            this.Type = type;
            this.ReducedType = reducedType;

            this.Value = Field != null ? Field.ToFunc() : Property != null ? Property.ToFunc() : item => null;
        }

        public readonly System.Reflection.FieldInfo Field;
        public readonly System.Reflection.PropertyInfo Property;

        public string Name => Field != null ? Field.Name : Property != null ? Property.Name : null;
        public readonly string InitialName;
        public readonly Type Type;
        public readonly Type ReducedType;

        public readonly Func<object, object> Value;
        //public object Value(object item)
        //{
        //  if (Field != null)
        //    return Field.GetValue(item);
        //  if (Property != null)
        //    return Property.GetValue(item, null);
        //  return null;
        //}

        public override string ToString()
        {
            return string.Format("{0}:{1}{2}", Name ?? InitialName, Type != null ? Type.Name : null,
                Type != ReducedType ? string.Format("({0})", ReducedType.Name) : null);
        }
    }
}