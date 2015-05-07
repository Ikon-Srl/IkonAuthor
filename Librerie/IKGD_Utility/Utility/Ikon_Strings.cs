/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2011 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.IO;
using System.Text;
using System.Web;
using System.Collections;
using System.Text.RegularExpressions;
using System.Diagnostics;


namespace Ikon
{

  // classi utilizzate da Utility.StringSimilarity


  internal delegate double StringSimilarity(string s1, string s2);


  internal class BipartiteMatcher
  {
    private string[] _leftTokens, _rightTokens;
    private double[,] _cost;
    private double[] leftLabel, rightLabel;
    private int[] _previous, _incomming, _outgoing; //connect with the left and right

    private bool[] _leftVisited, _rightVisited;
    int leftLen, rightLen;
    bool _errorOccured = false;

    public BipartiteMatcher(string[] left, string[] right, double[,] cost)
    {
      if (left == null || right == null || cost == null)
      {
        _errorOccured = true;
        return;
      }
      _leftTokens = left;
      _rightTokens = right;
      //swap
      if (_leftTokens.Length > _rightTokens.Length)
      {
        double[,] tmpCost = new double[_rightTokens.Length, _leftTokens.Length];
        for (int i = 0; i < _rightTokens.Length; i++)
          for (int j = 0; j < _leftTokens.Length; j++)
            tmpCost[i, j] = cost[j, i];

        _cost = (double[,])tmpCost.Clone();
        string[] tmp = _leftTokens;
        _leftTokens = _rightTokens;
        _rightTokens = tmp;
      }
      else
        _cost = (double[,])cost.Clone();
      MyInit();
      Make_Matching();
    }

    private void MyInit()
    {
      Initialize();
      _leftVisited = new bool[leftLen + 1];
      _rightVisited = new bool[rightLen + 1];
      _previous = new int[(leftLen + rightLen) + 2];
    }

    private void Initialize()
    {
      leftLen = _leftTokens.Length - 1;
      rightLen = _rightTokens.Length - 1;

      leftLabel = new double[leftLen + 1];
      rightLabel = new double[rightLen + 1];
      for (int i = 0; i < leftLabel.Length; i++) leftLabel[i] = 0;
      for (int i = 0; i < rightLabel.Length; i++) rightLabel[i] = 0;
      //init distance
      for (int i = 0; i <= leftLen; i++)
      {
        double maxLeft = double.MinValue;
        for (int j = 0; j <= rightLen; j++)
        {
          if (_cost[i, j] > maxLeft) maxLeft = _cost[i, j];
        }
        leftLabel[i] = maxLeft;
      }
    }

    private void Flush()
    {
      for (int i = 0; i < _previous.Length; i++) _previous[i] = -1;
      for (int i = 0; i < _leftVisited.Length; i++) _leftVisited[i] = false;
      for (int i = 0; i < _rightVisited.Length; i++) _rightVisited[i] = false;
    }

    bool stop = false;
    bool FindPath(int source)
    {
      Flush();
      stop = false;
      Walk(source);
      return stop;
    }

    void Increase_Matchs(int li, int lj)
    {
      int[] tmpOut = (int[])_outgoing.Clone();
      int i, j, k;
      i = li; j = lj;
      _outgoing[i] = j; _incomming[j] = i;
      if (_previous[i] != -1)
      {
        do
        {
          j = tmpOut[i];
          k = _previous[i];
          _outgoing[k] = j; _incomming[j] = k;
          i = k;
        } while (_previous[i] != -1);
      }
    }


    private void Walk(int i)
    {
      _leftVisited[i] = true;

      for (int j = 0; j <= rightLen; j++)
      {
        if (stop)
          return;
        else
        {
          if (!_rightVisited[j] && (leftLabel[i] + rightLabel[j] == _cost[i, j]))
          {
            if (_incomming[j] == -1)// if found a path
            {
              stop = true;
              Increase_Matchs(i, j);
              return;
            }
            else
            {
              int k = _incomming[j];
              _rightVisited[j] = true;
              _previous[k] = i;
              Walk(k);
            }
          }
        }
      }
    }


    double GetMinDeviation()
    {
      double min = double.MaxValue;

      for (int i = 0; i <= leftLen; i++)
      {
        if (_leftVisited[i])
        {
          for (int j = 0; j <= rightLen; j++)
          {
            if (!_rightVisited[j])
            {
              if (leftLabel[i] + rightLabel[j] - _cost[i, j] < min)
                min = (leftLabel[i] + rightLabel[j]) - _cost[i, j];
            }
          }
        }
      }
      return min;
    }

    private void Relabels()
    {
      double dev = GetMinDeviation();

      for (int k = 0; k <= leftLen; k++)
      {
        if (_leftVisited[k])
        {
          leftLabel[k] -= dev;
        }
      }
      for (int k = 0; k <= rightLen; k++)
      {
        if (_rightVisited[k])
        {
          rightLabel[k] += dev;
        }
      }
    }

    private void Make_Matching()
    {
      _outgoing = new int[leftLen + 1];
      _incomming = new int[rightLen + 1];
      for (int i = 0; i < _outgoing.Length; i++) _outgoing[i] = -1;
      for (int i = 0; i < _incomming.Length; i++) _incomming[i] = -1;

      for (int k = 0; k <= leftLen; k++)
      {
        if (_outgoing[k] == -1)
        {
          bool found = false;
          do
          {
            found = FindPath(k);
            if (!found) Relabels();

          } while (!found);
        }
      }
    }


    private double GetTotal()
    {
      double nTotal = 0;
      double nA = 0;
      Trace.Flush();
      for (int i = 0; i <= leftLen; i++)
      {
        if (_outgoing[i] != -1)
        {
          nTotal += _cost[i, _outgoing[i]];
          Trace.WriteLine(_leftTokens[i] + " <-> " + _rightTokens[_outgoing[i]] + " : " + _cost[i, _outgoing[i]]);
          double a = 1.0F - System.Math.Max(_leftTokens[i].Length, _rightTokens[_outgoing[i]].Length) != 0 ? _cost[i, _outgoing[i]] / System.Math.Max(_leftTokens[i].Length, _rightTokens[_outgoing[i]].Length) : 1;
          nA += a;
        }
      }
      return nTotal;
    }

    public double GetScore()
    {
      double dis = GetTotal();

      double maxLen = rightLen + 1;
      int l1 = 0; int l2 = 0;
      foreach (string s in _rightTokens) l1 += s.Length;
      foreach (string s in _leftTokens) l2 += s.Length;
      maxLen = Math.Max(l1, l2);

      if (maxLen > 0)
        return dis / maxLen;
      else
        return 1.0F;
    }


    public double Score
    {
      get
      {
        if (_errorOccured)
          return 0;
        else
          return GetScore();
      }
    }

  }



  internal class Leven
  {
    private int Min3(int a, int b, int c)
    {
      return System.Math.Min(System.Math.Min(a, b), c);
    }

    private int ComputeDistance(string s, string t)
    {
      int n = s.Length;
      int m = t.Length;
      int[,] distance = new int[n + 1, m + 1]; // matrix
      int cost = 0;

      if (n == 0) return m;
      if (m == 0) return n;
      //init1
      for (int i = 0; i <= n; distance[i, 0] = i++) ;
      for (int j = 0; j <= m; distance[0, j] = j++) ;

      //find min distance
      for (int i = 1; i <= n; i++)
      {
        for (int j = 1; j <= m; j++)
        {
          cost = (t.Substring(j - 1, 1) == s.Substring(i - 1, 1) ? 0 : 1);
          distance[i, j] = Min3(distance[i - 1, j] + 1,
              distance[i, j - 1] + 1,
              distance[i - 1, j - 1] + cost);
        }
      }

      return distance[n, m];
    }

    public double GetSimilarity(string string1, string string2)
    {

      double dis = ComputeDistance(string1, string2);
      double maxLen = string1.Length;
      if (maxLen < (double)string2.Length)
        maxLen = string2.Length;

      double minLen = string1.Length;
      if (minLen > (double)string2.Length)
        minLen = string2.Length;


      if (maxLen == 0.0F)
        return 1.0F;
      else
      {
        return maxLen - dis;
        //return 1.0F - dis/maxLen ;
        //return (double) Math.Round(1.0F - dis/maxLen, 1) * 10 ;
      }
    }

    public Leven()
    {
    }
  }



  internal class Tokeniser
  {
    public Regex tokenizerRx { get; set; }


    private ArrayList Tokenize(string input)
    {
      ArrayList returnVect = new ArrayList(10);
      int nextGapPos;
      for (int curPos = 0; curPos < input.Length; curPos = nextGapPos)
      {
        char ch = input[curPos];
        if (System.Char.IsWhiteSpace(ch))
          curPos++;
        nextGapPos = input.Length;
        for (int i = 0; i < "\r\n\t \x00A0".Length; i++)
        {
          int testPos = input.IndexOf((Char)"\r\n\t \x00A0"[i], curPos);
          if (testPos < nextGapPos && testPos != -1)
            nextGapPos = testPos;
        }
        string term = input.Substring(curPos, (nextGapPos) - (curPos));
        //if (!stopWordHandler.isWord(term))
        returnVect.Add(term);
      }

      return returnVect;
    }

    private void Normalize_Casing(ref string input)
    {
      //if it is formed by Pascal/Carmel casing
      for (int i = 0; i < input.Length; i++)
      {
        if (Char.IsSeparator(input[i]))
          input = input.Replace(input[i].ToString(), " ");
      }
      int idx = 1;
      while (idx < input.Length - 2)
      {
        ++idx;
        if (
            (Char.IsUpper(input[idx])
            && Char.IsLower(input[idx + 1]))
            &&
            (!Char.IsWhiteSpace(input[idx - 1]) && !Char.IsSeparator(input[idx - 1]))
            )
        {
          input = input.Insert(idx, " ");
          ++idx;
        }
      }
    }

    public string[] Partition(string input)
    {
      tokenizerRx = tokenizerRx ?? new Regex("([ \\t{}():;])");

      Normalize_Casing(ref input);
      //normalization to the lower case
      input = input.ToLower();

      String[] tokens = tokenizerRx.Split(input);

      ArrayList filter = new ArrayList();

      for (int i = 0; i < tokens.Length; i++)
      {
        MatchCollection mc = tokenizerRx.Matches(tokens[i]);
        if (mc.Count <= 0 && tokens[i].Trim().Length > 0)
          filter.Add(tokens[i]);

      }

      tokens = new string[filter.Count];
      for (int i = 0; i < filter.Count; i++) tokens[i] = (string)filter[i];

      return tokens;
    }


    public Tokeniser()
    {
    }
  }



  //
  // usage:
  // string.Format(FileSizeFormatProvider.Factory(), "{0:fs}", 12345678)
  //
  public class FileSizeFormatProvider : IFormatProvider, ICustomFormatter
  {
    public object GetFormat(Type formatType)
    {
      if (formatType == typeof(ICustomFormatter))
        return this;
      return null;
    }


    public static FileSizeFormatProvider Factory() { return new FileSizeFormatProvider(); }


    private const string fileSizeFormat = "fs";
    private const Decimal OneKiloByte = 1024M;
    private const Decimal OneMegaByte = OneKiloByte * 1024M;
    private const Decimal OneGigaByte = OneMegaByte * 1024M;

    public string Format(string format, object arg, IFormatProvider formatProvider)
    {
      if (format == null || !format.StartsWith(fileSizeFormat))
      {
        return defaultFormat(format, arg, formatProvider);
      }

      if (arg is string)
      {
        return defaultFormat(format, arg, formatProvider);
      }

      Decimal size;
      try
      {
        size = Convert.ToDecimal(arg);
      }
      catch (InvalidCastException)
      {
        return defaultFormat(format, arg, formatProvider);
      }

      string suffix;
      if (size > OneGigaByte)
      {
        size /= OneGigaByte;
        suffix = " GB";
      }
      else if (size > OneMegaByte)
      {
        size /= OneMegaByte;
        suffix = " MB";
      }
      else if (size > OneKiloByte)
      {
        size /= OneKiloByte;
        suffix = " KB";
      }
      else
      {
        suffix = " B";
      }

      string precision = format.Substring(2);
      if (String.IsNullOrEmpty(precision))
        precision = "2";
      return String.Format("{0:N" + precision + "}{1}", size, suffix);
    }


    private static string defaultFormat(string format, object arg, IFormatProvider formatProvider)
    {
      IFormattable formattableArg = arg as IFormattable;
      if (formattableArg != null)
      {
        return formattableArg.ToString(format, formatProvider);
      }
      return arg.ToString();
    }
  }



}
