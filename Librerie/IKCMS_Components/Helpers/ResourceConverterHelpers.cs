using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;
using System.Web.Routing;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Resources;


namespace Ikon.IKCMS
{


  public class IKCMS_ResourceConverterHelpers
  {
    public FS_Operations fsOp_ro { get { return IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly"); } }
    public FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }


    public string DumpTemplatesInfo()
    {
      var vdatas = fsOp.DB.IKGD_VDATAs.Where(r => r.flag_current == true || r.flag_published == true).ToList();
      var infos = vdatas.Select(r =>
      {
        string template = "NULL";
        var resource = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(r);
        if (resource != null)
        {
          var wdgData = resource.ResourceSettingsObject;
          if (wdgData is WidgetSettingsType_HasTemplateSelector_Interface)
          {
            template = (wdgData as WidgetSettingsType_HasTemplateSelector_Interface).TemplateType;
          }
        }
        return new { r.manager_type, r.category, template };
      }).ToList();
      StringBuilder sb = new StringBuilder();
      sb.AppendLine("<table>");
      sb.AppendFormat("<th><tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td></tr></th>\n", "ManagerType", "Category", "Template", "#");
      foreach (var grp in infos.GroupBy(r => r).OrderBy(g => g.Key.manager_type).ThenBy(g => g.Key.category).ThenBy(g => g.Key.template))
      {
        sb.AppendFormat("<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td></tr>\n", grp.Key.manager_type, grp.Key.category, grp.Key.template, grp.Count());
      }
      sb.AppendLine("</table>");
      return sb.ToString();
    }


    public string DumpMissingPlaceholders(int? version_frozen)
    {
      List<FS_Operations.FS_NodeInfo> nodesWithBrokenMapping = new List<FS_Operations.FS_NodeInfo>();
      using (FS_Operations fsOp = new FS_Operations(version_frozen))
      {
        //
        var manager_types_ty = IKCMS_RegisteredTypes.Types_IKCMS_BrickWithPlaceholder_Interface.ToList();
        var manager_types = manager_types_ty.Select(t => t.Name).ToList();
        var categories = fsOp.NodesActive<IKGD_VDATA>(true).Where(n => manager_types.Contains(n.manager_type)).Select(n => n.category).Distinct().ToList();
        var mappingLibrary = fsOp.NodesActive<IKGD_VDATA>(true).Where(n => manager_types.Contains(n.manager_type)).GroupBy(n => new { n.manager_type, n.category }).Select(g => g.Key).ToList().Select(r => new { type = manager_types_ty.FirstOrDefault(t => t.Name == r.manager_type), manager_type = r.manager_type, category = r.category, placeholders = new List<IKCMS_PageCMS_Placeholder_Interface>(), templates = new List<IKCMS_PageCMS_Template_Interface>() }).ToList();
        mappingLibrary.ForEach(m =>
        {
          var tmp_plh = IKCMS_TemplatesTypeHelper.PlaceholdersAvailableForResource(m.type, m.category, false);
          if (tmp_plh != null && tmp_plh.Any())
            m.placeholders.AddRange(tmp_plh);
          var tmp_tpl = IKCMS_TemplatesTypeHelper.TemplatesAvailableForResource(m.type, m.category, null, false);
          if (tmp_tpl != null && tmp_tpl.Any())
            m.templates.AddRange(tmp_tpl);
        });
        //
        var fsNodes =
          from vd in fsOp.NodesActive<IKGD_VDATA>(true).Where(n => manager_types.Contains(n.manager_type))
          from vn in fsOp.NodesActive<IKGD_VNODE>(true).Where(n => n.rnode == vd.rnode)
          select new FS_Operations.FS_NodeInfo { vNode = vn, vData = vd };
        //
        int num = fsNodes.Count();
        //
        foreach (var fsNodeGrp in fsNodes.GroupBy(n => n.vData.rnode))
        {
          var vData = fsNodeGrp.FirstOrDefault().vData;
          var map = mappingLibrary.FirstOrDefault(m => m.manager_type == vData.manager_type && m.category == vData.category);
          if (map == null)
            continue;
          foreach (var fsNode in fsNodeGrp)
          {
            if (fsNode.vNode.placeholder == null || !map.placeholders.Any(p => p.Code == fsNode.vNode.placeholder))
              nodesWithBrokenMapping.Add(fsNode);
          }
        }
        int num2 = nodesWithBrokenMapping.Count();
        //
      }

      StringBuilder sb = new StringBuilder();
      sb.AppendLine("<table>");
      sb.AppendFormat("<th><tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td></tr></th>\n", "rNode", "sNode", "ManagerType", "Category", "Placeholder");
      foreach (var grp in nodesWithBrokenMapping.GroupBy(r => r.rNode).OrderBy(g => g.Key))
      {
        foreach (var fsNode in grp.OrderBy(n => n.sNode))
          sb.AppendFormat("<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td></tr>\n", fsNode.rNode, fsNode.sNode, fsNode.ManagerType, fsNode.Category, fsNode.Placeholder);
      }
      sb.AppendLine("</table>");
      return sb.ToString();
    }


    public string ConvertCategoriesPlaceholdersVFS(bool? committ)
    {
      List<string> messages = new List<string>();
      try
      {
        if (!IKGD_Config.IsLocalRequestWrapper)
          throw new Exception("Richiesta proveniente da una connessione non autorizzata.");
        //
        fsOp.EnsureOpenConnection();
        using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
        {
          //
          var manager_types_CN = new List<string>() { typeof(IKCMS_FolderType_ArchiveRoot).Name, typeof(IKCMS_FolderType_FolderWeb).Name, typeof(IKGD_Folder_Folder).Name };
          fsOp.DB.IKGD_VDATAs.Where(n => manager_types_CN.Contains(n.manager_type) && n.category != null).ForEach(n => n.category = null);
          var chg = fsOp.DB.GetChangeSet();
          fsOp.DB.SubmitChanges();
          //
          XElement xMappings = Utility.FileReadXmlVirtual("~/Author/Config/ResourceConverter.xml");
          if (xMappings != null && xMappings.Descendants("map").Any())
          {
            var xMaps = xMappings.Elements("map").ToList();
            var manager_types = xMappings.Descendants("old").Select(x => x.AttributeValue("ManagerType")).Where(r => !string.IsNullOrEmpty(r)).Distinct().ToList();
            var vDatas = fsOp.DB.IKGD_VDATAs.Where(n => n.flag_published == true || n.flag_current == true).Where(n => manager_types.Contains(n.manager_type)).ToList();
            var vNodes = fsOp.DB.IKGD_VDATAs.Where(n => n.flag_published == true || n.flag_current == true).Where(n => manager_types.Contains(n.manager_type)).Join(fsOp.DB.IKGD_VNODEs.Where(n => n.flag_published == true || n.flag_current == true), vd => vd.rnode, vn => vn.rnode, (vd, vn) => vn).Distinct().ToList();
            foreach (var vDatasGrp in vDatas.GroupBy(n => n.rnode))
            {
              IKGD_VDATA vData = null;
              XElement xMap = null;
              foreach (var vDataGrp in vDatasGrp)
              {
                vData = vDataGrp;
                xMap = xMaps.FirstOrDefault(x => vData.manager_type == x.Element("old").AttributeValue("ManagerType") && vData.category == x.Element("old").AttributeValue("Category", vData.category));
                if (xMap != null)
                  break;
              }
              if (vData == null || xMap == null)
                continue;
              string manager_type = xMap.Element("new").AttributeValue("ManagerType", vData.manager_type);
              string category = xMap.Element("new").AttributeValue("Category", vData.category);
              string placeholder = xMap.Element("new").AttributeValue("Placeholder");
              //
              vDatasGrp.Where(n => n.manager_type != manager_type).ForEach(n => n.manager_type = manager_type);
              vDatasGrp.Where(n => n.category != category).ForEach(n => n.category = category);
              vNodes.Where(n => n.rnode == vDatasGrp.Key && n.placeholder != placeholder).ForEach(n => n.placeholder = placeholder);
              //
            }
            //
            var chg2 = fsOp.DB.GetChangeSet();
            messages.Add("vDatas: {0}".FormatString(vDatas.Count));
            messages.Add("vNodes: {0}".FormatString(vNodes.Count));
            messages.Add("Risorse modificate: {0}  vDatas:{1}  vNodes:{2}".FormatString(chg2.Updates.Count, chg2.Updates.OfType<IKGD_VDATA>().Count(), chg2.Updates.OfType<IKGD_VNODE>().Count()));
            fsOp.DB.SubmitChanges();
            //
          }
          //
          if (committ.GetValueOrDefault(false))
          {
            ts.Committ();
            messages.Add("Operazione completata, transazione finalizzata.");
          }
          else
          {
            messages.Add("Operazione completata, transazione non finalizzata.");
          }
        }
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message);
      }
      return Utility.Implode(messages, "<br/>\n");
    }


    public string ConvertTemplatesVFS(bool? committ, bool? verbose)
    {
      List<string> messages = new List<string>();
      try
      {
        if (!IKGD_Config.IsLocalRequestWrapper)
          throw new Exception("Richiesta proveniente da una connessione non autorizzata.");
        //
        fsOp.EnsureOpenConnection();
        using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
        {
          foreach (var version_frozen in new int[] { 0, -1 })
          {
            var fsNodes =
              from vd in fsOp.NodesActive<IKGD_VDATA>(true)
              from vn in fsOp.NodesActive<IKGD_VNODE>(true).Where(n => n.rnode == vd.rnode)
              select new FS_Operations.FS_NodeInfo { vNode = vn, vData = vd };
            foreach (var fsNodeGrp in fsNodes.GroupBy(n => n.vData.rnode))
            {
              try
              {
                var vData = fsNodeGrp.FirstOrDefault().vData;
                bool template_required = false;
                string template = null;
                var resource = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(vData);
                if (resource != null)
                {
                  var wdgData = resource.ResourceSettingsObject;
                  if (wdgData is WidgetSettingsType_HasTemplateSelector_Interface)
                  {
                    template_required = true;
                    template = (wdgData as WidgetSettingsType_HasTemplateSelector_Interface).TemplateType;
                  }
                }
                if (template_required)
                {
                  if (version_frozen == 0)
                  {
                    //fsNodeGrp.Where(n => n.vNode != null && n.vNode.template != template).ForEach(n => n.vNode.template = template);
                    foreach (var vNode in fsNodeGrp.Where(n => n.vNode != null && n.vNode.template != template))
                    {
                      if (verbose == true)
                        messages.Add(string.Format("sNode [{0}]  template: {1} --> {2}", vNode.vNode.snode, vNode.vNode.template, template));
                      vNode.vNode.template = template;
                    }
                  }
                  else
                  {
                    // per le risorse in preview che non sono gia' state processate come published devo procedere con un COW
                    foreach (var vNode in fsNodeGrp.Where(n => n.vNode != null && n.vNode.template != template).Select(n => n.vNode).ToList())
                    {
                      if (verbose == true)
                        messages.Add(string.Format("sNode duplicato [{0}]  template: {1} --> {2}", vNode.snode, vNode.template, template));
                      var vNodeDup = fsOp.CloneNode(vNode, true, true);
                      vNodeDup.template = template;
                      fsOp.DB.IKGD_VNODEs.InsertOnSubmit(vNodeDup);
                    }
                  }
                }
              }
              catch (Exception ex)
              {
                messages.Add(ex.Message);
              }
            }
            //
            var chg = fsOp.DB.GetChangeSet();
            messages.Add("Risorse modificate:  insert={1}   update={0}".FormatString(chg.Updates.Count, chg.Inserts.Count));
            fsOp.DB.SubmitChanges();
            //
          }
          //
          if (committ.GetValueOrDefault(false))
          {
            ts.Committ();
            messages.Add("Operazione completata, transazione finalizzata.");
          }
          else
          {
            messages.Add("Operazione completata, transazione non finalizzata.");
          }
        }
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message);
      }
      return Utility.Implode(messages, "<br/>\n");
    }


    public string NormalizeCategoriesPlaceholdersTemplatesVFS(bool? committ, int? version_frozen)
    {
      if (!IKGD_Config.IsLocalRequestWrapper)
        throw new Exception("Richiesta proveniente da una connessione non autorizzata.");
      //
      using (FS_Operations fsOp = new FS_Operations(version_frozen))
      {
        fsOp.EnsureOpenConnection();
        using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(7200))
        {
          //
          var manager_types_CN = new List<string>() { typeof(IKCMS_FolderType_ArchiveRoot).Name, typeof(IKCMS_FolderType_FolderWeb).Name, typeof(IKGD_Folder_Folder).Name };
          fsOp.DB.IKGD_VDATAs.Where(n => manager_types_CN.Contains(n.manager_type) && n.category != null).ForEach(n => n.category = null);
          var chg = fsOp.DB.GetChangeSet();
          fsOp.DB.SubmitChanges();
          //
          var manager_types_ty = IKCMS_RegisteredTypes.Types_IKCMS_BrickWithPlaceholder_Interface.ToList();
          var manager_types = manager_types_ty.Select(t => t.Name).ToList();
          var categories = fsOp.NodesActive<IKGD_VDATA>(true).Where(n => manager_types.Contains(n.manager_type)).Select(n => n.category).Distinct().ToList();
          var mappingLibrary = fsOp.NodesActive<IKGD_VDATA>(true).Where(n => manager_types.Contains(n.manager_type)).GroupBy(n => new { n.manager_type, n.category }).Select(g => g.Key).ToList().Select(r => new { type = manager_types_ty.FirstOrDefault(t => t.Name == r.manager_type), manager_type = r.manager_type, category = r.category, placeholders = new List<IKCMS_PageCMS_Placeholder_Interface>(), templates = new List<IKCMS_PageCMS_Template_Interface>() }).ToList();
          mappingLibrary.ForEach(m =>
          {
            var tmp_plh = IKCMS_TemplatesTypeHelper.PlaceholdersAvailableForResource(m.type, m.category, false);
            if (tmp_plh != null && tmp_plh.Any())
              m.placeholders.AddRange(tmp_plh);
            var tmp_tpl = IKCMS_TemplatesTypeHelper.TemplatesAvailableForResource(m.type, m.category, null, false);
            if (tmp_tpl != null && tmp_tpl.Any())
              m.templates.AddRange(tmp_tpl);
          });
          //
          var fsNodes =
            from vd in fsOp.NodesActive<IKGD_VDATA>(true).Where(n => manager_types.Contains(n.manager_type))
            from vn in fsOp.NodesActive<IKGD_VNODE>(true).Where(n => n.rnode == vd.rnode)
            select new FS_Operations.FS_NodeInfo { vNode = vn, vData = vd };
          //
          int num = fsNodes.Count();
          //
          int chunkSize = 25;
          foreach (var fsNodeGrpChunk in fsNodes.GroupBy(n => n.vData.rnode).Slice(chunkSize))
          {
            //
            // filtriamo i nodi trovati con RootVFS
            //
            var rNodes = fsNodeGrpChunk.Select(g => g.Key).ToList();
            var paths = fsOp.PathsFromNodesAuthorEquiv(null, rNodes, false).FilterPathsByRootsAuthor().ToList();
            foreach (var fsNodeGrp in fsNodes.GroupBy(n => n.vData.rnode))
            {
              if (!paths.Any(p => p.rNode == fsNodeGrp.Key))
                continue;
              var sNodes = paths.Where(p => p.rNode == fsNodeGrp.Key).Select(p => p.sNode).Distinct().ToList();
              var vData = fsNodeGrp.FirstOrDefault().vData;
              var vNodes = fsNodeGrp.Where(n => sNodes.Contains(n.sNode));
              if (!vNodes.Any())
                continue;
              var map = mappingLibrary.FirstOrDefault(m => m.manager_type == vData.manager_type && m.category == vData.category);
              if (map == null)
                continue;
              bool template_required = false;
              string template = null;
              var resource = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(vData);
              if (resource != null)
              {
                var wdgData = resource.ResourceSettingsObject;
                if (wdgData is WidgetSettingsType_HasTemplateSelector_Interface)
                {
                  template_required = true;
                  template = (wdgData as WidgetSettingsType_HasTemplateSelector_Interface).TemplateType;
                }
              }
              if (template_required && ((string.IsNullOrEmpty(template) && map.templates.Count > 1) || !map.templates.Any(t => t.TemplateType == template)))
              {
                // e' necessario assegnare un template perche' null e abbiamo piu' di un template disponibile oppure il valore specificato non e' valido
                //map.templates.Where(t=>t.Selectable && (t.Placeholders.Contains())
              }


            }
          }
          //
          if (committ.GetValueOrDefault(false))
          {
            ts.Committ();
            return "Operazione completata, transazione finalizzata.";
          }
          else
          {
            return "Operazione completata, transazione non finalizzata.";
          }
        }
      }
    }


    public string MigrateVDATA_data_field(bool? committ)
    {
      if (!IKGD_Config.IsLocalRequestWrapper)
        throw new Exception("Richiesta proveniente da una connessione non autorizzata.");
      //
      using (FS_Operations fsOp = new FS_Operations())
      {
        fsOp.EnsureOpenConnection();
        using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
        {
          //
          // per attivare questo modulo di conversione e' necessario riattivare il supporto per IKGD_VDATA.data in IKGD_DataClasses.dbml
          //
          return "Operazione non completata modulo con codice commentate.";
          /*
          //
          var manager_types = new List<string>() { typeof(Ikon.IKGD.Library.Resources.IKGD_Widget_iGoogle).Name };
          var vDatas = fsOp.DB.IKGD_VDATAs.Where(n => manager_types.Contains(n.manager_type) && n.data != null).ToList();
          //
          foreach (var vData in vDatas)
          {
            //var wdg = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(vData);
            var wdg = IKGD_WidgetDataBase.DeSerializeByType(typeof(Ikon.IKGD.Library.Resources.IKGD_Widget_iGoogle.IKGD_WidgetData_iGoogle), vData.settings, false);
            (wdg.Config as Ikon.IKGD.Library.Resources.IKGD_Widget_iGoogle.IKGD_WidgetData_iGoogle.ClassConfig_iGoogle).XmlGadget = Utility.LinqBinaryGetStringDB(vData.data);
            vData.settings = wdg.Serialize();
          }
          //
          var chg = fsOp.DB.GetChangeSet();
          fsOp.DB.SubmitChanges();
          //
          if (committ.GetValueOrDefault(false))
          {
            ts.Committ();
            return "Operazione completata migrate {0} risorse, transazione finalizzata.".FormatString(chg.Updates.Count);
          }
          else
          {
            return "Operazione completata migrate {0} risorse, transazione non finalizzata.".FormatString(chg.Updates.Count);
          }
          //
          */
        }
      }
    }

  }
}
