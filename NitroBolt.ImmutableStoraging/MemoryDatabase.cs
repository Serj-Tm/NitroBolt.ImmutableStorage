using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NitroBolt.ImmutableStoraging
{
  public class MemoryDatabase<T>:IMemoryDatabase 
    where T:class, IWorld, new()
  {
    public MemoryDatabase(Dictionary<Type, string> ids, Dictionary<Type, Dictionary<string, string>> references, Dictionary<Type, Dictionary<string, PushInfo[]>> pushes,
      Dictionary<string, Dictionary<object, object>> data, string dataPath = null)
    {
      this.Serializer = new QSerializer(ids, references, pushes);
      this.Data = data;

      this.DataPath = dataPath ?? HttpContext.Current.Server.MapPath(string.Format("~/App_Data"));
    }
    /// <summary>
    /// Путь к папке, где хранятся save-ы, по умолчанию App_Data
    /// </summary>
    public readonly string DataPath = null;

    public QSerializer Serializer { get; protected set; }
    public Dictionary<string, Dictionary<object, object>> Data { get; protected set; }

    public T World
    {
      get
      {
        lock (locker)
        {
          if (_World == null)
            _World = Load() ?? new T();
          return _World;
        }
      }
    }
    public T Change(Func<T, T> f)
    {
      lock (locker)
      {
        if (_World == null)
          _World = Load() ?? new T();
        _World = f(_World);
      }
      Save(_World);
      return _World;
    }
    public void Reset()
    {
      lock (locker)
      {
        _World = null;
      }
    }
    readonly object locker = new object();
    T _World = null;


    public void Save(T world)
    {
      var text = Serializer.Save(world)?.ToString();

      System.IO.File.WriteAllText(System.IO.Path.Combine(DataPath, Filename(world.Tick)), text);

      var text2 = Serializer.Save(LoadFromText(text))?.ToString();
      if (text != text2)
        System.IO.File.WriteAllText(System.IO.Path.Combine(DataPath, Filename(world.Tick, isError:true)), text2);


    }

    private static string Filename(int tick, bool isError = false)
    {
      return string.Format("q{0}.{1:0000000}.qs", isError ? "_err":null, tick);
    }

    public T Load(int? version = null)
    {
      var filename = version != null ? System.IO.Path.Combine(DataPath, Filename(version.Value)) : System.IO.Directory.GetFiles(DataPath, "q.*.qs").Max();
      if (filename == null)
        return null;

      var text = System.IO.File.ReadAllText(filename);
      
      return LoadFromText(text);
    }

    public T LoadFromText(string text)
    {
      var q = QSharp.QParser.Parse(text).FirstOrDefault();
      
      return (T)Serializer.Load(typeof(T), q, Data);
    }



    object IMemoryDatabase.World
    {
      get { return World; }
    }
  }
  public interface IWorld
  {
    int Tick { get; }
  }
  public interface IMemoryDatabase
  {
    QSerializer Serializer { get; }
    Dictionary<string, Dictionary<object, object>> Data { get; }
    object World { get; }
  }
}