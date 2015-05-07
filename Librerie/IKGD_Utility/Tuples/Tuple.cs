using System;
using System.Diagnostics;



namespace Ikon
{

  public static class TupleW
  {

    public static TupleW<T1, T2> From<T1, T2>(T1 first, T2 second)
    {
      return new TupleW<T1, T2>(first, second);
    }


    public static TupleW<T1, T2, T3> From<T1, T2, T3>(T1 first, T2 second, T3 third)
    {
      return new TupleW<T1, T2, T3>(first, second, third);
    }


    public static TupleW<T1, T2, T3, T4> From<T1, T2, T3, T4>(T1 first, T2 second, T3 third, T4 fourth)
    {
      return new TupleW<T1, T2, T3, T4>(first, second, third, fourth);
    }


    public static TupleW<T1, T2, T3> Append<T1, T2, T3>(this TupleW<T1, T2> tuple, T3 item)
    {
      return TupleW.From(tuple.Item1, tuple.Item2, item);
    }


    public static TupleW<T1, T2, T3, T4> Append<T1, T2, T3, T4>(this TupleW<T1, T2, T3> tuple, T4 item)
    {
      return TupleW.From(tuple.Item1, tuple.Item2, tuple.Item3, item);
    }



    public static int GetHashCode(params object[] args)
    {
      unchecked
      {
        int result = 0;
        foreach (var o in args)
          result = (result * 397) ^ (o != null ? o.GetHashCode() : 0);
        return result;
      }
    }

  }


  [Serializable]
  [DebuggerDisplay("({Item1},{Item2})")]
  public class TupleW<T1, T2> : IEquatable<TupleW<T1, T2>>
  {
    public T1 Item1 { get; set; }
    public T2 Item2 { get; set; }

    public TupleW() { }
    public TupleW(T1 first, T2 second)
    {
      Item1 = first;
      Item2 = second;
    }


    public override bool Equals(object obj)
    {
      if (ReferenceEquals(null, obj))
        throw new NullReferenceException("obj is null");
      if (ReferenceEquals(this, obj)) return true;
      if (!(obj is TupleW<T1, T2>)) return false;
      return Equals((TupleW<T1, T2>)obj);
    }


    public override string ToString()
    {
      return string.Format("({0},{1})", Item1, Item2);
    }


    public bool Equals(TupleW<T1, T2> obj)
    {
      if (ReferenceEquals(null, obj)) return false;
      if (ReferenceEquals(this, obj)) return true;
      return Equals(obj.Item1, Item1) && Equals(obj.Item2, Item2);
    }


    public override int GetHashCode()
    {
      return TupleW.GetHashCode(Item1, Item2);
    }

    public static bool operator ==(TupleW<T1, T2> left, TupleW<T1, T2> right)
    {
      return Equals(left, right);
    }


    public static bool operator !=(TupleW<T1, T2> left, TupleW<T1, T2> right)
    {
      return !Equals(left, right);
    }

  }


  [Serializable]
  [DebuggerDisplay("({Item1},{Item2},{Item3})")]
  public class TupleW<T1, T2, T3> : IEquatable<TupleW<T1, T2, T3>>
  {
    public T1 Item1 { get; set; }
    public T2 Item2 { get; set; }
    public T3 Item3 { get; set; }


    public TupleW() { }
    public TupleW(T1 first, T2 second, T3 third)
    {
      Item1 = first;
      Item2 = second;
      Item3 = third;
    }


    public override string ToString()
    {
      return string.Format("({0},{1},{2})", Item1, Item2, Item3);
    }


    public override bool Equals(object obj)
    {
      if (ReferenceEquals(null, obj))
        throw new NullReferenceException("obj is null");
      if (ReferenceEquals(this, obj)) return true;
      if (!(obj is TupleW<T1, T2, T3>)) return false;
      return Equals((TupleW<T1, T2, T3>)obj);
    }


    public bool Equals(TupleW<T1, T2, T3> obj)
    {
      if (ReferenceEquals(null, obj)) return false;
      if (ReferenceEquals(this, obj)) return true;
      return Equals(obj.Item1, Item1) && Equals(obj.Item2, Item2) && Equals(obj.Item3, Item3);
    }


    public override int GetHashCode()
    {
      return TupleW.GetHashCode(Item1, Item2, Item3);
    }


    public static bool operator ==(TupleW<T1, T2, T3> left, TupleW<T1, T2, T3> right)
    {
      return Equals(left, right);
    }


    public static bool operator !=(TupleW<T1, T2, T3> left, TupleW<T1, T2, T3> right)
    {
      return !Equals(left, right);
    }

  }

  
  [Serializable]
  [DebuggerDisplay("({Item1},{Item2},{Item3},{Item4})")]
  public class TupleW<T1, T2, T3, T4> : IEquatable<TupleW<T1, T2, T3, T4>>
  {
    public T1 Item1 { get; set; }
    public T2 Item2 { get; set; }
    public T3 Item3 { get; set; }
    public T4 Item4 { get; set; }


    public TupleW() { }
    public TupleW(T1 first, T2 second, T3 third, T4 fourth)
    {
      Item1 = first;
      Item2 = second;
      Item3 = third;
      Item4 = fourth;
    }


    public override string ToString()
    {
      return string.Format("({0},{1},{2},{3})", Item1, Item2, Item3, Item4);
    }


    public override bool Equals(object obj)
    {
      if (ReferenceEquals(null, obj))
        throw new NullReferenceException("obj is null");
      if (ReferenceEquals(this, obj)) return true;
      if (!(obj is TupleW<T1, T2, T3, T4>)) return false;
      return Equals((TupleW<T1, T2, T3, T4>)obj);
    }


    public bool Equals(TupleW<T1, T2, T3, T4> obj)
    {
      if (ReferenceEquals(null, obj)) return false;
      if (ReferenceEquals(this, obj)) return true;
      return Equals(obj.Item1, Item1)
          && Equals(obj.Item2, Item2)
              && Equals(obj.Item3, Item3)
                  && Equals(obj.Item4, Item4);
    }


    public override int GetHashCode()
    {
      return TupleW.GetHashCode(Item1, Item2, Item3, Item4);
    }


    public static bool operator ==(TupleW<T1, T2, T3, T4> left, TupleW<T1, T2, T3, T4> right)
    {
      return Equals(left, right);
    }


    public static bool operator !=(TupleW<T1, T2, T3, T4> left, TupleW<T1, T2, T3, T4> right)
    {
      return !Equals(left, right);
    }

  }


}