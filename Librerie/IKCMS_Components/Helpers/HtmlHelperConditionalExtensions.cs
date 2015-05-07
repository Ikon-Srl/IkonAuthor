using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;


namespace Ikon.IKCMS
{

  public static class HtmlHelperConditionalExtensions
  {

    // simpler version
    public static void PartialIf(this HtmlHelper htmlHelper, bool condition, Action<HtmlHelper> action)
    {
      if (condition)
      {
        action.Invoke(htmlHelper);
      }
    }


    /// <summary>
    /// Begins a conditional rendering statement
    /// </summary>
    /// <param name="helper"></param>
    /// <param name="condition"></param>
    /// <param name="ifAction"></param>
    /// <returns></returns>
    public static ConditionalHtmlRender If(this HtmlHelper helper, bool condition, Func<HtmlHelper, string> ifAction)
    {
      return new ConditionalHtmlRender(helper, condition, ifAction);
    }


    public class ConditionalHtmlRender
    {
      private readonly HtmlHelper _helper;
      private readonly bool _ifCondition;
      private readonly Func<HtmlHelper, string> _ifAction;
      private readonly Dictionary<bool, Func<HtmlHelper, string>> _elseActions = new Dictionary<bool, Func<HtmlHelper, string>>();

      public ConditionalHtmlRender(HtmlHelper helper, bool ifCondition, Func<HtmlHelper, string> ifAction)
      {
        _helper = helper;
        _ifCondition = ifCondition;
        _ifAction = ifAction;
      }

      /// <summary>
      /// Ends the conditional block with an else branch
      /// </summary>
      /// <param name="renderAction"></param>
      /// <returns></returns>
      public ConditionalHtmlRender Else(Func<HtmlHelper, string> renderAction)
      {
        return ElseIf(true, renderAction);
      }

      /// <summary>
      /// Adds an else if branch to the conditional block
      /// </summary>
      /// <param name="condition"></param>
      /// <param name="renderAction"></param>
      /// <returns></returns>
      public ConditionalHtmlRender ElseIf(bool condition, Func<HtmlHelper, string> renderAction)
      {
        _elseActions.Add(condition, renderAction);
        return this;
      }

      public override string ToString()
      {
        if (_ifCondition) // if the IF condition is true, render IF action
        {
          return _ifAction.Invoke(_helper);
        }
        // find the first ELSE condition that is true, and render it
        foreach (KeyValuePair<bool, Func<HtmlHelper, string>> elseAction in _elseActions)
        {
          if (elseAction.Key)
          {
            return elseAction.Value.Invoke(_helper);
          }
        }
        // no condition true; render nothing
        return String.Empty;
      }
    }
  }


}

