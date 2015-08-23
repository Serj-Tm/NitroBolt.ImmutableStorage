using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Linq.Expressions;

namespace NitroBolt.ImmutableStoraging
{
  public static class ExpressionHlp
  {
    public static Func<object[], object> ToFunc(this ConstructorInfo constructor, ParameterInfo[] parameters = null)
    {
      if (parameters == null)
        parameters = constructor.GetParameters();

      var p = Expression.Parameter(typeof(object[]), "values");
      var args = new Expression[parameters.Length];
      for (var i = 0; i < parameters.Length; ++i)
        args[i] = Expression.Convert(Expression.ArrayIndex(p, Expression.Constant(i)), parameters[i].ParameterType);
      return Expression.Lambda<Func<object[], object>>(Expression.New(constructor, args), p).Compile();
    }
    public static Func<object, object> ToFunc(this FieldInfo field)
    {
      var p = Expression.Parameter(typeof(object), "item");
      return Expression.Lambda<Func<object, object>>(Expression.Convert(Expression.Field(Expression.Convert(p, field.DeclaringType), field), typeof(object)), p).Compile();
    }
    public static Func<object, object> ToFunc(this PropertyInfo property)
    {
      var p = Expression.Parameter(typeof(object), "item");
      return Expression.Lambda<Func<object, object>>(Expression.Convert(Expression.Property(Expression.Convert(p, property.DeclaringType), property), typeof(object)), p).Compile();
    }
  }
}