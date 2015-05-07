using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using LinqKit;

using Ikon;
using Ikon.Config;
using Ikon.GD;
using Ikon.IKGD.Library;



namespace Ikon.IKGD_VFS_Utils
{

  //
  // TODO:
  // aggiungere un controllo sulle aree utilizzate nel VFS: che non ve ne siano di non definite
  // aggiungere un controllo sui manager_type che non ve ne siano di non supportati dalle librerie
  //
  // TODO:EXTERNALSTORAGE gestire le operazioni di pulizia dell'external storage (con pulsante apposito e action MVC)
  //

  public class VFS_UtilsCheckDB
  {
    public FS_Operations fsOp { get; protected set; }
    public FS_Operations fsOpP { get; protected set; }  //fsOp per risorse pubblicate
    public FS_Operations fsOpX { get; protected set; }  //fsOp per risorse preview

    public static readonly string LostFoundFolderName = "Lost+Found";


    public VFS_UtilsCheckDB()
    {
    }


    public List<string> VfsCheckNodesStructure(bool completeTransaction)
    {
      List<string> messages = new List<string>();
      try
      {
        int folderLostFound = LostFound_EnsureFolder().Value;
        using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
        {
          using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
          {
            fsOp = _fsOp;
            fsOp.DB.CommandTimeout = 3600;
            //
            // cancellazione dei locks
            //
            fsOp.DB.IKGD_RNODEs.Where(n => n.locked != null || n.locked_by != null).ForEach(n => { n.locked = null; n.locked_by = null; });
            var chg01a = fsOp.DB.GetChangeSet();
            messages.Add("Normalized locks: {0} -> normalized".FormatString(chg01a.Updates.Count));
            fsOp.DB.SubmitChanges();
            //
            fsOp.DB.IKGD_VDATAs.Where(n => n.manager_type == null).ForEach(n => n.manager_type = string.Empty);
            var chg01b = fsOp.DB.GetChangeSet();
            messages.Add("Normalized manager_type: {0} -> normalized".FormatString(chg01b.Updates.Count));
            fsOp.DB.SubmitChanges();
            //
            bool useFullCOW = true;
            if (useFullCOW == false)
            {
              fsOp.DB.IKGD_VDATAs.Where(n => n.flag_current == false && n.flag_deleted == true).ForEach(n => n.flag_deleted = false);
              fsOp.DB.IKGD_VNODEs.Where(n => n.flag_current == false && n.flag_deleted == true).ForEach(n => n.flag_deleted = false);
              fsOp.DB.IKGD_INODEs.Where(n => n.flag_current == false && n.flag_deleted == true).ForEach(n => n.flag_deleted = false);
              fsOp.DB.IKGD_PROPERTies.Where(n => n.flag_current == false && n.flag_deleted == true).ForEach(n => n.flag_deleted = false);
              fsOp.DB.IKGD_RELATIONs.Where(n => n.flag_current == false && n.flag_deleted == true).ForEach(n => n.flag_deleted = false);
              var chg01c = fsOp.DB.GetChangeSet();
              messages.Add("Normalized flag_deleted without flag_current: {0} -> normalized".FormatString(chg01c.Updates.Count));
              fsOp.DB.SubmitChanges();
            }
            //
            // relations con rNodes non validi che devono essere normalizzati
            //
            {
              var mismatch_src =
                from node_rel in fsOp.DB.IKGD_RELATIONs
                from node_sn in fsOp.DB.IKGD_SNODEs.Where(n => n.code == node_rel.snode_src && n.rnode != node_rel.rnode)
                select new { node_rel, node_sn };
              var mismatch_dst =
                from node_rel in fsOp.DB.IKGD_RELATIONs
                from node_sn in fsOp.DB.IKGD_SNODEs.Where(n => n.code == node_rel.snode_dst && n.rnode != node_rel.rnode_dst)
                select new { node_rel, node_sn };
              mismatch_src.ForEach(r => r.node_rel.rnode = r.node_sn.rnode);
              mismatch_dst.ForEach(r => r.node_rel.rnode_dst = r.node_sn.rnode);
              var chg01d = fsOp.DB.GetChangeSet();
              messages.Add("Normalized relations with mismatch in rnode_src or rnode_dst: {0} -> normalized".FormatString(chg01d.Updates.Count));
              fsOp.DB.SubmitChanges();
            }
            //
            // risorse con rNode == 0 che derivano sicuramente da qualche problema in salvataggio
            //
            fsOp.NodesActive<IKGD_VNODE>(-1, false).Where(n => n.rnode == 0).ForEach(n => n.flag_current = false);
            fsOp.NodesActive<IKGD_INODE>(-1, false).Where(n => n.rnode == 0).ForEach(n => n.flag_current = false);
            fsOp.NodesActive<IKGD_VDATA>(-1, false).Where(n => n.rnode == 0).ForEach(n => n.flag_current = false);
            fsOp.NodesActive<IKGD_PROPERTY>(-1, false).Where(n => n.rnode == 0).ForEach(n => n.flag_current = false);
            fsOp.NodesActive<IKGD_RELATION>(-1, false).Where(n => n.rnode == 0 || n.rnode_dst == 0).ForEach(n => n.flag_current = false);
            fsOp.NodesActive<IKGD_VNODE>(0, false).Where(n => n.rnode == 0).ForEach(n => n.flag_published = false);
            fsOp.NodesActive<IKGD_INODE>(0, false).Where(n => n.rnode == 0).ForEach(n => n.flag_published = false);
            fsOp.NodesActive<IKGD_VDATA>(0, false).Where(n => n.rnode == 0).ForEach(n => n.flag_published = false);
            fsOp.NodesActive<IKGD_PROPERTY>(0, false).Where(n => n.rnode == 0).ForEach(n => n.flag_published = false);
            fsOp.NodesActive<IKGD_RELATION>(0, false).Where(n => n.rnode == 0 || n.rnode_dst == 0).ForEach(n => n.flag_published = false);
            var chg02 = fsOp.DB.GetChangeSet();
            messages.Add("Nodes (active) with rnode == 0 -> inactivated: {0}".FormatString(chg02.Updates.Count));
            fsOp.DB.SubmitChanges();
            //
            // risorse con rNode == 0 che derivano sicuramente da qualche problema in salvataggio (pulizia completa)
            //
            fsOp.DB.IKGD_VNODEs.DeleteAllOnSubmit(fsOp.DB.IKGD_VNODEs.Where(n => n.snode == 0));
            fsOp.DB.IKGD_VNODEs.DeleteAllOnSubmit(fsOp.DB.IKGD_VNODEs.Where(n => n.rnode == 0));
            fsOp.DB.IKGD_INODEs.DeleteAllOnSubmit(fsOp.DB.IKGD_INODEs.Where(n => n.rnode == 0));
            fsOp.DB.IKGD_VDATAs.DeleteAllOnSubmit(fsOp.DB.IKGD_VDATAs.Where(n => n.rnode == 0));
            fsOp.DB.IKGD_PROPERTies.DeleteAllOnSubmit(fsOp.DB.IKGD_PROPERTies.Where(n => n.rnode == 0));
            fsOp.DB.IKGD_RELATIONs.DeleteAllOnSubmit(fsOp.DB.IKGD_RELATIONs.Where(n => n.rnode == 0 || n.rnode_dst == 0));
            var chg03 = fsOp.DB.GetChangeSet();
            messages.Add("Nodes (all) with rnode == 0: {0} -> deleted".FormatString(chg03.Deletes.Count));
            fsOp.DB.SubmitChanges();
            //
            // normalizzazione di nodi preview con version minore di quelle published
            //
            fsOp.NodesActive<IKGD_VDATA>(0, true).Join(fsOp.NodesActive<IKGD_VDATA>(-1, true), np => np.rnode, nc => nc.rnode, (np, nc) => new { np, nc }).Where(r => r.np.version > r.nc.version).ForEach(r => { r.np.flag_current = true; r.nc.flag_current = false; });
            fsOp.NodesActive<IKGD_INODE>(0, true).Join(fsOp.NodesActive<IKGD_INODE>(-1, true), np => np.rnode, nc => nc.rnode, (np, nc) => new { np, nc }).Where(r => r.np.version > r.nc.version).ForEach(r => { r.np.flag_current = true; r.nc.flag_current = false; });
            fsOp.NodesActive<IKGD_VNODE>(0, true).Join(fsOp.NodesActive<IKGD_VNODE>(-1, true), np => np.snode, nc => nc.snode, (np, nc) => new { np, nc }).Where(r => r.np.version > r.nc.version).ForEach(r => { r.np.flag_current = true; r.nc.flag_current = false; });
            var chg04a = fsOp.DB.GetChangeSet();
            messages.Add("Nodes with preview.version < published.version: {0} -> preview promoted to published".FormatString(chg04a.Updates.Count));
            fsOp.DB.SubmitChanges();
            //
            // normalizzazione published/preview multipli
            //
            fsOp.NodesActive<IKGD_VNODE>(-1, false).GroupBy(n => n.snode).Where(g => g.Count() > 1).ForEach(g => g.OrderByDescending(n => n.version).Skip(1).ForEach(n => n.flag_current = false));
            fsOp.NodesActive<IKGD_VDATA>(-1, false).GroupBy(n => n.rnode).Where(g => g.Count() > 1).ForEach(g => g.OrderByDescending(n => n.version).Skip(1).ForEach(n => n.flag_current = false));
            fsOp.NodesActive<IKGD_INODE>(-1, false).GroupBy(n => n.rnode).Where(g => g.Count() > 1).ForEach(g => g.OrderByDescending(n => n.version).Skip(1).ForEach(n => n.flag_current = false));
            fsOp.NodesActive<IKGD_VNODE>(0, false).GroupBy(n => n.snode).Where(g => g.Count() > 1).ForEach(g => g.OrderByDescending(n => n.version).Skip(1).ForEach(n => n.flag_published = false));
            fsOp.NodesActive<IKGD_VDATA>(0, false).GroupBy(n => n.rnode).Where(g => g.Count() > 1).ForEach(g => g.OrderByDescending(n => n.version).Skip(1).ForEach(n => n.flag_published = false));
            fsOp.NodesActive<IKGD_INODE>(0, false).GroupBy(n => n.rnode).Where(g => g.Count() > 1).ForEach(g => g.OrderByDescending(n => n.version).Skip(1).ForEach(n => n.flag_published = false));
            var chg04b = fsOp.DB.GetChangeSet();
            messages.Add("Nodes with multiple preview/published status set: {0} -> inactivated/normalized".FormatString(chg04b.Updates.Count));
            fsOp.DB.SubmitChanges();
            //
            // eliminazione di broken nodes senza mapping corretto con vNode
            //
            foreach (int version in new int[] { -1, 0 })
            {
              // vData senza vNode
              (from vData in fsOp.NodesActive<IKGD_VDATA>(version, false)
               where !fsOp.NodesActive<IKGD_VNODE>(version, false).Any(n => n.rnode == vData.rnode)
               select vData).ToList().ForEach(n =>
               {
                 if (version == 0)
                   n.flag_published = false;
                 else
                   n.flag_deleted = true;
               });
              // vNode senza vData
              (from vNode in fsOp.NodesActive<IKGD_VNODE>(version, false)
               where !fsOp.NodesActive<IKGD_VDATA>(version, false).Any(n => n.rnode == vNode.rnode)
               select vNode).ToList().ForEach(n =>
               {
                 if (version == 0)
                   n.flag_published = false;
                 else
                   n.flag_deleted = true;
               });
              // iNode senza vData
              (from iNode in fsOp.NodesActive<IKGD_INODE>(version, false)
               where !fsOp.NodesActive<IKGD_VDATA>(version, false).Any(n => n.rnode == iNode.rnode)
               select iNode).ToList().ForEach(n =>
               {
                 if (version == 0)
                   n.flag_published = false;
                 else
                   n.flag_deleted = true;
               });
              // properties senza vData
              (from node in fsOp.NodesActive<IKGD_PROPERTY>(version, false)
               where !fsOp.NodesActive<IKGD_VDATA>(version, false).Any(n => n.rnode == node.rnode)
               select node).ToList().ForEach(n =>
               {
                 if (version == 0)
                   n.flag_published = false;
                 else
                   n.flag_deleted = true;
               });
              // relations senza vData src o dst
              (from node in fsOp.NodesActive<IKGD_RELATION>(version, false)
               where !fsOp.NodesActive<IKGD_VDATA>(version, false).Any(n => n.rnode == node.rnode) || !fsOp.NodesActive<IKGD_VDATA>(version, false).Any(n => n.rnode == node.rnode_dst)
               select node).ToList().ForEach(n =>
               {
                 if (version == 0)
                   n.flag_published = false;
                 else
                   n.flag_deleted = true;
               });
              //
              // relations con sNode src o dst non corretto
              //
              var weakRelationBroken =
                (from relation in fsOp.NodesActive<IKGD_RELATION>(version, false)
                 where fsOp.NodesActive<IKGD_VDATA>(version, false).Any(n => n.rnode == relation.rnode) && fsOp.NodesActive<IKGD_VDATA>(version, false).Any(n => n.rnode == relation.rnode_dst)
                 where !fsOp.NodesActive<IKGD_VNODE>(version, false).Any(n => n.snode == relation.snode_src) || !fsOp.NodesActive<IKGD_VNODE>(version, false).Any(n => n.snode == relation.snode_dst)
                 select new { relation, vNodeSrc = fsOp.NodesActive<IKGD_VNODE>(version, false).FirstOrDefault(n => n.snode == relation.snode_src), vNodeDst = fsOp.NodesActive<IKGD_VNODE>(version, false).FirstOrDefault(n => n.snode == relation.snode_dst) }).ToList();
              foreach (var relationData in weakRelationBroken)
              {
                if (relationData.vNodeSrc == null)
                {
                  try { relationData.relation.snode_src = fsOp.NodesActive<IKGD_VNODE>(version, false).Where(n => n.rnode == relationData.relation.rnode).OrderByDescending(n => n.version).FirstOrDefault().snode; }
                  catch { }
                }
                if (relationData.vNodeDst == null)
                {
                  try { relationData.relation.snode_dst = fsOp.NodesActive<IKGD_VNODE>(version, false).Where(n => n.rnode == relationData.relation.rnode_dst).OrderByDescending(n => n.version).FirstOrDefault().snode; }
                  catch { }
                }
              }
              //
              var chg05 = fsOp.DB.GetChangeSet();
              messages.Add("Nodes with missing components/mapping/relations (version={1}): {0} -> inactivated/normalized".FormatString(chg05.Updates.Count, version));
              fsOp.DB.SubmitChanges();
            }
            //
            // gestione dei missing nodes coinvolti in frammenti preview con cancellazioni
            //
            {
              var brokenDeleted =
                (from rnode in fsOp.DB.IKGD_RNODEs.Where(r => r.IKGD_SNODEs.Any())
                 join n in fsOp.NodesActive<IKGD_VDATA>(-1, true) on rnode.code equals n.rnode into vDatas
                 join n in fsOp.NodesActive<IKGD_VNODE>(-1, true) on rnode.code equals n.rnode into vNodes
                 join n in fsOp.NodesActive<IKGD_INODE>(-1, true) on rnode.code equals n.rnode into iNodes
                 join n in fsOp.NodesActive<IKGD_PROPERTY>(-1, true) on rnode.code equals n.rnode into properties
                 join n in fsOp.NodesActive<IKGD_RELATION>(-1, true) on rnode.code equals n.rnode into relations
                 where (vDatas.Any(n => n.flag_deleted) || vNodes.Any(n => n.flag_deleted) || iNodes.Any(n => n.flag_deleted) || properties.Any(n => n.flag_deleted) || relations.Any(n => n.flag_deleted))  // almeno uno dei frammenti deve essere cancellato
                 where (vDatas.Any() && !vNodes.Any()) || !vDatas.Any()  // deve mancare almeno il vNode o il vData
                 select new { vDatas = vDatas.ToList(), vNodes = vNodes.ToList(), iNodes = iNodes.ToList(), properties = properties.ToList(), relations = relations.ToList() }).ToList();
              brokenDeleted.ForEach(rec =>
              {
                if (rec.vDatas.Any())
                {
                  // non ci sono vNodes associati
                  rec.vDatas.ForEach(n => n.flag_current = false);
                  rec.iNodes.ForEach(n => n.flag_current = false);
                  rec.properties.ForEach(n => n.flag_current = false);
                  rec.relations.ForEach(n => n.flag_current = false);
                  //
                  //rec.vDatas.ForEach(n => n.flag_current = n.flag_deleted = false);
                  //rec.iNodes.ForEach(n => n.flag_current = n.flag_deleted = false);
                  //rec.properties.ForEach(n => n.flag_current = n.flag_deleted = false);
                  //rec.relations.ForEach(n => n.flag_current = n.flag_deleted = false);
                }
                else
                {
                  // in tutti i seguenti casi e' mancante il vData
                  rec.vNodes.ForEach(n => n.flag_current = false);
                  rec.iNodes.ForEach(n => n.flag_current = false);
                  rec.properties.ForEach(n => n.flag_current = false);
                  rec.relations.ForEach(n => n.flag_current = false);
                  //
                  //rec.vNodes.ForEach(n => n.flag_current = n.flag_deleted = false);
                  //rec.iNodes.ForEach(n => n.flag_current = n.flag_deleted = false);
                  //rec.properties.ForEach(n => n.flag_current = n.flag_deleted = false);
                  //rec.relations.ForEach(n => n.flag_current = n.flag_deleted = false);
                }
              });
              //
              var chg06 = fsOp.DB.GetChangeSet();
              messages.Add("Nodes with missing components/mapping/relations (preview+deleted): {0} -> inactivated/normalized".FormatString(chg06.Updates.Count));
              fsOp.DB.SubmitChanges();
            }
            //
            // nodi pubblicati senza un corrispettivo preview (anche cancellato)
            //
            {
              var brokenPublished =
                (from snode in fsOp.DB.IKGD_SNODEs
                 join n in fsOp.NodesActive<IKGD_VDATA>(0, false) on snode.rnode equals n.rnode into vDatasP
                 join n in fsOp.NodesActive<IKGD_VDATA>(-1, true) on snode.rnode equals n.rnode into vDatasX
                 join n in fsOp.NodesActive<IKGD_VNODE>(0, false) on snode.code equals n.snode into vNodesP
                 join n in fsOp.NodesActive<IKGD_VNODE>(-1, true) on snode.code equals n.snode into vNodesX
                 where vDatasP.Any() && vNodesP.Any()
                 where !vDatasX.Any() || !vNodesX.Any()
                 select new { vDatasP = vDatasP.ToList(), vDatasX = vDatasX.ToList(), vNodesP = vNodesP.ToList(), vNodesX = vNodesX.ToList() }).ToList();
              brokenPublished.ForEach(rec =>
              {
                //ricerca di risorse published senza un corrispettivo preview (eccetto che in /Lost+Found)
                bool IsLostFound = !rec.vNodesP.Any(n => (n.flag_folder && n.parent == folderLostFound) || (!n.flag_folder && n.folder == folderLostFound));
                if (!IsLostFound)
                {
                  if (!rec.vDatasX.Any())
                  {
                    rec.vDatasP.ForEach(n => n.flag_current = n.flag_deleted = true);
                  }
                  if (!rec.vNodesX.Any())
                  {
                    rec.vNodesP.ForEach(n => n.flag_current = n.flag_deleted = true);
                  }
                }
              });
              //
              var chg07 = fsOp.DB.GetChangeSet();
              messages.Add("Nodes published with missing counterpart (not in /Lost+Found): {0} -> normalized".FormatString(chg07.Updates.Count));
              fsOp.DB.SubmitChanges();
            }
            //
            // IKGD_SNODE con snode settato e rnode == 0
            //
            {
              var brokenSnodes =
                from snode in fsOp.DB.IKGD_SNODEs.Where(n => n.rnode == 0)
                join n in fsOp.NodesActive<IKGD_VNODE>() on snode.code equals n.snode into vnodes1
                join n in fsOp.DB.IKGD_VNODEs on snode.code equals n.snode into vnodes2
                where vnodes1.Any(n => n.rnode != 0) || vnodes2.Any(n => n.rnode != 0)
                select new { snode, vnode = vnodes1.FirstOrDefault() ?? vnodes2.FirstOrDefault() };
              brokenSnodes.ForEach(n => n.snode.rnode = n.vnode.rnode);
              //
              var chg08 = fsOp.DB.GetChangeSet();
              messages.Add("sNodes with rnode==0: {0} -> normalized".FormatString(chg08.Updates.Count));
              fsOp.DB.SubmitChanges();
            }
            //
            // IKGD_Property e IKGD_Relation potrebbero avere version_frozen differenti dopo la pubblicazione forzata
            //
            {
              var broken_properties = fsOp.NodesActive<IKGD_PROPERTY>(-1, true).Where(r => r.version_frozen != null).GroupBy(r => r.rnode).Where(g => g.Select(r => r.version_frozen.Value).Distinct().Count() > 1);
              var broken_relations = fsOp.NodesActive<IKGD_RELATION>(-1, true).Where(r => r.version_frozen != null).GroupBy(r => r.rnode).Where(g => g.Select(r => r.version_frozen.Value).Distinct().Count() > 1);
              foreach (var grp in broken_properties)
              {
                var v_max = grp.Max(r => r.version_frozen.Value);
                grp.Where(r => r.version_frozen != v_max).ForEach(r => r.version_frozen = v_max);
              }
              foreach (var grp in broken_relations)
              {
                var v_max = grp.Max(r => r.version_frozen.Value);
                grp.Where(r => r.version_frozen != v_max).ForEach(r => r.version_frozen = v_max);
              }
              //
              var chg09 = fsOp.DB.GetChangeSet();
              messages.Add("properties and relations with mismatched version_frozen: {0} -> normalized".FormatString(chg09.Updates.Count));
              fsOp.DB.SubmitChanges();
            }
            //
            // Normalizzazione di IKCMS_SEO
            //
            {
              fsOp.DB.IKCMS_SEOs.Where(r => r.target_snode != null).Join(fsOp.DB.IKGD_SNODEs, r => r.target_snode.Value, n => n.code, (seo, node) => new { seo, node }).Where(r => r.seo.target_rnode == null || r.seo.target_rnode != r.node.rnode).ForEach(r => r.seo.target_rnode = r.node.rnode);
              var chg10 = fsOp.DB.GetChangeSet();
              messages.Add("IKCMS_SEO con target_rnode aggiornato: {0}".FormatString(chg10.Updates.Count));
              fsOp.DB.SubmitChanges();
              //
              // cancellazione dei record senza mapping validi su sNode
              fsOp.DB.IKCMS_SEOs.DeleteAllOnSubmit(fsOp.DB.IKCMS_SEOs.Where(r => r.target_snode != null && r.target_rnode == null).GroupJoin(fsOp.DB.IKGD_VNODEs.Where(n => n.flag_published || n.flag_current), r => r.target_snode.Value, n => n.snode, (seo, nodes) => new { seo, nodes }).Where(r => !r.nodes.Any()).Select(r => r.seo));
              fsOp.DB.IKCMS_SEOs.DeleteAllOnSubmit(fsOp.DB.IKCMS_SEOs.Where(r => r.target_rnode != null).GroupJoin(fsOp.DB.IKGD_VNODEs.Where(n => n.flag_published || n.flag_current), r => r.target_rnode.Value, n => n.rnode, (seo, nodes) => new { seo, nodes }).Where(r => !r.nodes.Any()).Select(r => r.seo));
              var chg11 = fsOp.DB.GetChangeSet();
              messages.Add("IKCMS_SEO eliminati perchè con target_rnode non valido o target_snode non validi: {0}".FormatString(chg11.Deletes.Count));
              fsOp.DB.SubmitChanges();
              //
              // cancellazione dei record senza mapping validi
              fsOp.DB.IKCMS_SEOs.DeleteAllOnSubmit(fsOp.DB.IKCMS_SEOs.Where(r => r.target_snode != null && r.target_rnode == null).Join(fsOp.DB.IKGD_VNODEs.Where(n => n.flag_published || n.flag_current), r => r.target_snode.Value, n => n.snode, (seo, node) => seo));
              var chg12 = fsOp.DB.GetChangeSet();
              messages.Add("IKCMS_SEO eliminati perchè senza target_rnode e con target_snode non valido: {0}".FormatString(chg12.Deletes.Count));
              fsOp.DB.SubmitChanges();
            }
            //
            var chg = fsOp.DB.GetChangeSet();
            fsOp.DB.SubmitChanges();
          }
          if (completeTransaction)
            ts.Committ();
        }
      }
      catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }
      return messages;
    }


    //
    // creazione di /Lost+Found se mancante
    // viene creato come published
    //
    public int? LostFound_EnsureFolder()
    {
      try
      {
        using (FS_Operations _fsOpX = new FS_Operations(-1, false, true, true))
        {
          using (FS_Operations _fsOpP = new FS_Operations(0, false, true, false))
          {
            fsOp = _fsOpX;
            //int root = _fsOpX.GetRootNodes(true).FirstOrDefault().rNode;
            int root = IKGD_ConfigVFS.ConfigExt.RootsCMS_Paths.Select(p => p.FirstFragment.rNode).FirstOrDefault();
            var fsNodeP = _fsOpP.Get_NodesInfoFiltered(vn => vn.flag_folder == true && vn.parent.Value == root && vn.name == LostFoundFolderName, null, null, FS_Operations.FilterVFS.Disabled | FS_Operations.FilterVFS.ACL).FirstOrDefault();
            var fsNodeX = _fsOpX.Get_NodesInfoFiltered(vn => vn.flag_folder == true && vn.parent.Value == root && vn.name == LostFoundFolderName, null, null, FS_Operations.FilterVFS.Disabled | FS_Operations.FilterVFS.ACL | FS_Operations.FilterVFS.Deleted).FirstOrDefault();
            var fsNode = fsNodeX ?? fsNodeP;
            if (fsNode == null)
            {
              var vNode = _fsOpX.COW_NewFolder(root, LostFoundFolderName, null);
              fsNode = _fsOpX.Get_NodeInfo(vNode.snode, null);
              //
              IKGD_SNAPSHOT freeze = new IKGD_SNAPSHOT { date_frozen = DateTime.Now, flag_published = false, flag_rejected = false, username = fsOp.CurrentUser, snode_root = 1, snode_folder = 1, affected = 0, name = "root", path = "/" };
              _fsOpX.DB.IKGD_SNAPSHOTs.InsertOnSubmit(freeze);
              _fsOpX.DB.SubmitChanges();
              int version_frozen = freeze.version_frozen;
              fsNode.vNode.flag_published = true;
              fsNode.vData.flag_published = true;
              fsNode.vNode.version_frozen = version_frozen;
              fsNode.vData.version_frozen = version_frozen;
              fsNode.vData.FlagsMenu |= FlagsMenuEnum.HiddenNode;
            }
            try
            {
              if (fsNode.vNode.flag_deleted)
                fsNode.vNode.flag_deleted = false;
              if (fsNodeP.sNode != fsNode.sNode)
                fsNodeP.vNode.flag_published = fsNodeP.vNode.flag_current = fsNodeP.vNode.flag_deleted = false;
              if (fsNodeP.rNode != fsNode.rNode)
                fsNodeP.vData.flag_published = fsNodeP.vData.flag_current = fsNodeP.vData.flag_deleted = false;
            }
            catch { }
            //
            _fsOpX.DB.SubmitChanges();
            _fsOpP.DB.SubmitChanges();
            //
            return fsNode.Folder;
          }
        }
      }
      catch { return null; }
    }


    public List<string> LostFound_SendTo(bool completeTransaction)
    {
      List<string> messages = new List<string>();
      try
      {
        //
        // creazione di /Lost+Found se mancante
        //
        int folderLostFound = LostFound_EnsureFolder().Value;
        //
        using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
        {
          using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
          {
            fsOp = _fsOp;
            fsOp.DB.CommandTimeout = 3600;
            //
            // popolamento di /Lost+Found
            //
            foreach (int version in new int[] { 0, -1 })
            {
              //
              // tutti i folder senza parent valido associato (esclusi i nodi root)
              // e tutti i nodi (resource) senza folder associato
              //
              var nodes =
                (from vNode in fsOp.NodesActive<IKGD_VNODE>(version, true).Where(n => (n.flag_folder == true && n.parent != 0) || n.flag_folder == false)
                 where !fsOp.NodesActive<IKGD_VNODE>(version, false).Any(n => (vNode.flag_folder == true && n.flag_folder == true && n.folder == vNode.parent) || (vNode.flag_folder == false && n.flag_folder == true && n.folder == vNode.folder))
                 select vNode).ToList();
              foreach (var node in nodes)
              {
                var nodeToUpdate = node;
                if (node.flag_published == true && node.flag_published == true)
                {
                  //prima di ricollegare i nodi che sono sia published che preview devo eseguire un COW
                  var nodeDup = fsOp.CloneNode(node, true, true);
                  nodeDup.flag_deleted = node.flag_deleted;
                  fsOp.DB.IKGD_VNODEs.InsertOnSubmit(nodeDup);
                  if (version == -1)
                    nodeToUpdate = nodeDup;
                }
                if (nodeToUpdate.flag_folder)
                  nodeToUpdate.parent = folderLostFound;
                else
                  nodeToUpdate.folder = folderLostFound;
              }

              //
              // tutti i folder senza parent valido associato (esclusi i nodi root)
              //
              //(from vNode in fsOp.NodesActive<IKGD_VNODE>(version, true).Where(n => n.flag_folder == true).Where(n => n.parent != 0)
              // where !fsOp.NodesActive<IKGD_VNODE>(version, false).Any(n => n.flag_folder == true && n.folder == vNode.parent)
              // select vNode).ToList().ForEach(n => n.parent = folderLostFound);
              //
              // tutti i nodi (resource) senza folder associato
              //
              //(from vNode in fsOp.NodesActive<IKGD_VNODE>(version, true).Where(n => n.flag_folder == false)
              // where !fsOp.NodesActive<IKGD_VNODE>(version, false).Any(n => n.flag_folder == true && n.folder == vNode.folder)
              // select vNode).ToList().ForEach(n => n.folder = folderLostFound);
            }
            //
            var chg = fsOp.DB.GetChangeSet();
            messages.Add("Nodes moved/dup to /Lost+Found: {0}/{1}".FormatString(chg.Updates.Count, chg.Inserts.Count));
            fsOp.DB.SubmitChanges();
          }
          if (completeTransaction)
            ts.Committ();
        }
      }
      catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }
      return messages;
    }


    //
    // normalizeParentsMismatchToPreview = null --> nessun processing
    // normalizeParentsMismatchToPreview = true --> published remapped to current
    // normalizeParentsMismatchToPreview = false --> current remapped to published
    //
    public List<string> LostFound_Processor(bool completeTransaction, bool? processLostAndFound, bool? normalizeParentsMismatchToPreview, bool? normalizeHiddenVersion, bool? normalizeMissingCurrents)
    {
      List<string> messages = new List<string>();
      try
      {
        //
        // creazione di /Lost+Found se mancante
        //
        int folderLostFound = LostFound_EnsureFolder().Value;
        //
        using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
        {
          using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
          {
            fsOp = _fsOp;
            fsOp.DB.CommandTimeout = 3600;
            //
            // gestione di /Lost+Found e problemi vari relativi al parenting
            //
            var nodes_active_preview = fsOp.NodesActive<IKGD_VNODE>(-1, true).Join(fsOp.NodesActive<IKGD_VDATA>(-1, true), n => n.rnode, n => n.rnode, (vnode, vdata) => vnode);
            var nodes_active_published = fsOp.NodesActive<IKGD_VNODE>(0, false).Join(fsOp.NodesActive<IKGD_VDATA>(0, false), n => n.rnode, n => n.rnode, (vnode, vdata) => vnode);
            var nodes_join =
              from nodeR in nodes_active_preview.Union(nodes_active_published).Where(n => (n.flag_folder == true && n.parent != 0) || n.flag_folder == false)
              from nodeP in nodes_active_preview.Union(nodes_active_published).Where(n => n.flag_folder).Where(r => (nodeR.flag_folder == true && nodeR.parent == r.rnode) || (nodeR.flag_folder == false && nodeR.folder == r.rnode)).DefaultIfEmpty()
              where nodeP == null || (nodeP.flag_published == nodeR.flag_published) || (nodeP.flag_current == nodeR.flag_current)
              select new { nodeR, nodeP };
            //var nodes_join_exp = nodes_join.ToList();

            //
            // migrazione dei nodi senza alcun parent in lost+found
            //
            if (processLostAndFound.GetValueOrDefault(false))
            {
              var nodes_join_lost = nodes_join.GroupBy(r => r.nodeR.snode).Where(g => g.Any(r => r.nodeP == null)).SelectMany(g => g).ToList();
              //var tmp01 = nodes_join_lost.Select(r => r.nodeR).Distinct().ToList();
              {
                int count = 0;
                foreach (var node in nodes_join_lost.Select(r => r.nodeR).Distinct())
                {
                  if (node.flag_folder)
                    node.parent = folderLostFound;
                  else
                    node.folder = folderLostFound;
                  if (++count <= 100)
                  {
                    messages.Add(" - node {0}/{1} restored to lost+found: {2}".FormatString(node.snode, node.rnode, node.name));
                  }
                }
              }
              //
              // nodes with implicit recursion
              //
              {
                int count = 0;
                var recursive_nodes = fsOp.NodesActive<IKGD_VNODE>(-1, true).Where(n => n.rnode == n.parent).Union(fsOp.NodesActive<IKGD_VNODE>(0, true).Where(n => n.rnode == n.parent)).ToList();
                foreach (var node in recursive_nodes)
                {
                  if (node.flag_folder)
                    node.parent = folderLostFound;
                  if (++count <= 100)
                  {
                    messages.Add(" - recursive node {0}/{1} moved to lost+found: {2}".FormatString(node.snode, node.rnode, node.name));
                  }
                }
              }
              //
              var chg = fsOp.DB.GetChangeSet();
              messages.Add("Nodes moved to /Lost+Found: {0}/{1}".FormatString(chg.Updates.Count, chg.Inserts.Count));
              fsOp.DB.SubmitChanges();
            }

            //
            // gestione dei nodi che hanno un mismatch nel parent
            //
            if (normalizeParentsMismatchToPreview != null)
            {
              var nodes_join_splitted = nodes_join.Where(r => r.nodeP != null).AsEnumerable().GroupBy(r => r.nodeR.snode).Where(g => g.Select(r => r.nodeP.rnode).Distinct().Count() > 1).SelectMany(g => g).ToList();
              //var tmp01 = nodes_join_splitted.Select(r => r.nodeR.snode).Distinct().ToList();
              //var tmp02 = nodes_join_splitted.Select(r => r.nodeR).Distinct().GroupBy(n => n.snode).OrderByDescending(g => g.Count()).Select(r => "[{0}] {1}".FormatString(r.Count(), r.Key)).ToList();
              foreach (var nodeset in nodes_join_splitted.Select(r => r.nodeR).Distinct().GroupBy(n => n.snode))
              {
                var master = nodeset.FirstOrDefault(r => normalizeParentsMismatchToPreview.GetValueOrDefault(false) ? r.flag_current : r.flag_published);
                if (master != null)
                {
                  var path = fsOp.PathsFromNodeAuthor(master.snode, true, false, true).FirstOrDefault();
                  foreach (var node in nodeset.Where(n => n != master))
                  {
                    if (master.flag_folder)
                    {
                      if (node.parent != master.parent)
                      {
                        messages.Add(" - node {0}/{1}:[{2}][{3}] moved from {4} to {5} -> {6}".FormatString(node.snode, node.rnode, node.flag_published, node.flag_current, node.parent, master.parent, path));
                        node.parent = master.parent;
                      }
                    }
                    else
                    {
                      if (node.folder != master.folder)
                      {
                        messages.Add(" - node {0}/{1}:[{2}][{3}] moved from {4} to {5} -> {6}".FormatString(node.snode, node.rnode, node.flag_published, node.flag_current, node.folder, master.folder, path));
                        node.folder = master.folder;
                      }
                    }
                  }
                }
              }
              var chg = fsOp.DB.GetChangeSet();
              messages.Add("Nodes reparented: {0}/{1}".FormatString(chg.Updates.Count, chg.Inserts.Count));
              fsOp.DB.SubmitChanges();
            }

            //
            // gestione dei nodi solo preview che hanno dei child published non visibili
            //
            if (normalizeHiddenVersion.GetValueOrDefault(false))
            {
              var nodes_join_hidden = nodes_join.Where(r => r.nodeP != null).AsEnumerable().GroupBy(r => r.nodeR.snode).Where(g => !((g.Any(r => r.nodeR.flag_current) ? g.Any(r => r.nodeP.flag_current) : true) && (g.Any(r => r.nodeR.flag_published) ? g.Any(r => r.nodeP.flag_published) : true))).SelectMany(g => g).ToList();
              //var tmp01 = nodes_join_hidden.Select(r => r.nodeR.snode).Distinct().ToList();
              var nodes_join_hidden2process = nodes_join_hidden.Where(r => r.nodeR.flag_published == true && r.nodeP.flag_published == false).Select(r => r.nodeP).Distinct().ToList();
              foreach (var nodeset in nodes_join_hidden2process.GroupBy(n => n.rnode))
              {
                int rNode = nodeset.Key;
                foreach (var node in nodeset.Where(n => n.flag_published == false && !n.flag_deleted))
                {
                  node.flag_published = true;
                  var path = fsOp.PathsFromNodeAuthor(node.snode, true, false, true).FirstOrDefault();
                  messages.Add(" - node {0}/{1}:[{2}][{3}] activated -> {4}".FormatString(node.snode, node.rnode, node.flag_published, node.flag_current, path));
                }
                List<string> frags = new List<string>();
                var vDatas = fsOp.DB.IKGD_VDATAs.Where(n => n.rnode == rNode).Where(n => n.flag_published || n.flag_current).ToList();
                var iNodes = fsOp.DB.IKGD_INODEs.Where(n => n.rnode == rNode).Where(n => n.flag_published || n.flag_current).ToList();
                var relations = fsOp.DB.IKGD_RELATIONs.Where(n => n.rnode == rNode).Where(n => n.flag_published || n.flag_current).ToList();
                var properties = fsOp.DB.IKGD_PROPERTies.Where(n => n.rnode == rNode).Where(n => n.flag_published || n.flag_current).ToList();
                if (!vDatas.Any(n => n.flag_published))
                {
                  vDatas.Where(n => n.flag_current && !n.flag_deleted).ForEach(n => n.flag_published = true);
                  frags.Add("vData");
                }
                if (!iNodes.Any(n => n.flag_published))
                {
                  iNodes.Where(n => n.flag_current && !n.flag_deleted).ForEach(n => n.flag_published = true);
                  frags.Add("iNode");
                }
                if (!relations.Any(n => n.flag_published))
                {
                  relations.Where(n => n.flag_current && !n.flag_deleted).ForEach(n => n.flag_published = true);
                  frags.Add("relations");
                }
                if (!properties.Any(n => n.flag_published))
                {
                  properties.Where(n => n.flag_current && !n.flag_deleted).ForEach(n => n.flag_published = true);
                  frags.Add("properties");
                }
                if (frags.Any())
                {
                  messages.Add(" - records: {0}".FormatString(Utility.Implode(frags, ",")));
                }
              }
              var chg = fsOp.DB.GetChangeSet();
              messages.Add("missing published nodes with published children autogenerated: {0}/{1}".FormatString(chg.Updates.Count, chg.Inserts.Count));
              fsOp.DB.SubmitChanges();
            }
            //
            // ricerca dei nodi che sono presenti solo in modalita' pubblicato e non in modalita' preview
            // se non si tratta di risorse cancellate ci deve essere qualche errore
            //
            if (normalizeMissingCurrents.GetValueOrDefault(false))
            {
              //
              var vDatas = fsOp.NodesActive<IKGD_VDATA>(0, false).Where(n1 => !fsOp.NodesActive<IKGD_VDATA>(-1, true).Any(n2 => n2.rnode == n1.rnode)).ToList();
              var vNodes = fsOp.NodesActive<IKGD_VNODE>(0, false).Where(n1 => !fsOp.NodesActive<IKGD_VNODE>(-1, true).Any(n2 => n2.snode == n1.snode)).ToList();
              //
              // da spostare in lost+found e settare come preview
              vDatas.Where(r => r.flag_current == false).ForEach(r => r.flag_current = true);
              foreach (var node in vNodes)
              {
                node.flag_current = true;
                if (node.flag_folder)
                  node.parent = folderLostFound;
                else
                  node.folder = folderLostFound;
              }
              //
              var chg04c = fsOp.DB.GetChangeSet();
              messages.Add("Nodes with missing preview: {0} -> moved to Lost+found".FormatString(chg04c.Updates.Count));
              fsOp.DB.SubmitChanges();
            }
            //
          }
          if (completeTransaction)
            ts.Committ();
        }
      }
      catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }
      messages.Add(string.Empty);
      return messages;
    }


    public List<string> LostFound_Cleanup(bool completeTransaction)
    {
      List<string> messages = new List<string>();
      try
      {
        //
        // creazione di /Lost+Found se mancante
        //
        int folderLostFound = LostFound_EnsureFolder().Value;
        //
        using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
        {
          using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
          {
            fsOp = _fsOp;
            fsOp.DB.CommandTimeout = 3600;
            //
            // popolamento di /Lost+Found
            //
            foreach (int version in new int[] { 0, -1 })
            {
              //
              // tutti i nodi che stanno in Lost+Found e non hanno qualche altro riferimento in giro
              //
              var lostData =
                (from vNode in fsOp.NodesActive<IKGD_VNODE>(version, true).Where(n => (n.flag_folder == true && n.parent == folderLostFound) || (n.flag_folder == false && n.folder == folderLostFound))
                 join n in fsOp.NodesActive<IKGD_VDATA>(version, true) on vNode.rnode equals n.rnode into vDatas
                 join n in fsOp.NodesActive<IKGD_VNODE>(version, true) on vNode.rnode equals n.rnode into vNodes
                 join n in fsOp.NodesActive<IKGD_INODE>(version, true) on vNode.rnode equals n.rnode into iNodes
                 join n in fsOp.NodesActive<IKGD_PROPERTY>(version, true) on vNode.rnode equals n.rnode into properties
                 join n in fsOp.NodesActive<IKGD_RELATION>(version, true) on vNode.rnode equals n.rnode into relations
                 select new { vDatas = vDatas.ToList(), vNodes = vNodes.ToList(), iNodes = iNodes.ToList(), properties = properties.ToList(), relations = relations.ToList() }).ToList();
              foreach (var record in lostData)
              {
                if (record.vNodes.Any(n => (n.flag_folder == true && n.parent != folderLostFound) || (n.flag_folder == false && n.folder != folderLostFound)))
                {
                  // pulizia del solo vNode in Lost+Found
                  record.vNodes.Where(n => (n.flag_folder == true && n.parent == folderLostFound) || (n.flag_folder == false && n.folder == folderLostFound)).ForEach(n => { if (version == 0) { n.flag_published = false; } else { n.flag_current = false; } });
                  //record.vNodes.Where(n => (n.flag_folder == true && n.parent == folderLostFound) || (n.flag_folder == false && n.folder == folderLostFound)).ForEach(n => { if (version == 0) { n.flag_published = false; } else { n.flag_current = n.flag_deleted = false; } });
                }
                else
                {
                  // pulizia totale della risorsa in quanto non ci sono riferimenti in giro per il VFS
                  record.vNodes.ForEach(n => { if (version == 0) { n.flag_published = false; } else { n.flag_current = false; } });
                  record.vDatas.ForEach(n => { if (version == 0) { n.flag_published = false; } else { n.flag_current = false; } });
                  record.iNodes.ForEach(n => { if (version == 0) { n.flag_published = false; } else { n.flag_current = false; } });
                  record.properties.ForEach(n => { if (version == 0) { n.flag_published = false; } else { n.flag_current = false; } });
                  record.relations.ForEach(n => { if (version == 0) { n.flag_published = false; } else { n.flag_current = false; } });
                  //
                  //record.vDatas.ForEach(n => { if (version == 0) { n.flag_published = false; } else { n.flag_current = n.flag_deleted = false; } });
                  //record.vNodes.ForEach(n => { if (version == 0) { n.flag_published = false; } else { n.flag_current = n.flag_deleted = false; } });
                  //record.iNodes.ForEach(n => { if (version == 0) { n.flag_published = false; } else { n.flag_current = n.flag_deleted = false; } });
                  //record.properties.ForEach(n => { if (version == 0) { n.flag_published = false; } else { n.flag_current = n.flag_deleted = false; } });
                  //record.relations.ForEach(n => { if (version == 0) { n.flag_published = false; } else { n.flag_current = n.flag_deleted = false; } });
                }
              }
              //
              var chg = fsOp.DB.GetChangeSet();
              messages.Add("Nodes/Folders wiped from /Lost+Found: {0} with version={1} for a total of {2} nodes".FormatString(lostData.Count, version, chg.Updates.Count));
              fsOp.DB.SubmitChanges();
            }
          }
          if (completeTransaction)
            ts.Committ();
        }
        //
        // con il nuovo schema di gestione del lost+found non dobbiamo far partire automaticamente un nuovo scan alla fine della pulizia
        //messages.Add("Performing a new Lost+Found check after Lost+Found wipe.");
        //messages.AddRange(LostFound_SendTo(completeTransaction));
      }
      catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }
      return messages;
    }


    public List<string> ClearInActiveNodes(bool completeTransaction, bool clearFrozenInactiveNodes)
    {
      List<string> messages = new List<string>();
      try
      {
        //
        int chunkSize = 500;
        DateTime start = DateTime.Now;
        int db_timeout = 3600;
        //
        try
        {
          using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(db_timeout))
          {
            using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
            {
              fsOp = _fsOp;
              fsOp.DB.CommandTimeout = db_timeout;
              //
              // cancellazione dei nodi non freezed e non attivi
              //
              fsOp.DB.IKGD_VNODEs.DeleteAllOnSubmit(fsOp.DB.IKGD_VNODEs.Where(n => n.version_frozen == null && !(n.flag_published == true || n.flag_current == true)));
              fsOp.DB.IKGD_INODEs.DeleteAllOnSubmit(fsOp.DB.IKGD_INODEs.Where(n => n.version_frozen == null && !(n.flag_published == true || n.flag_current == true)));
              fsOp.DB.IKGD_VDATAs.DeleteAllOnSubmit(fsOp.DB.IKGD_VDATAs.Where(n => n.version_frozen == null && !(n.flag_published == true || n.flag_current == true)));
              fsOp.DB.IKGD_PROPERTies.DeleteAllOnSubmit(fsOp.DB.IKGD_PROPERTies.Where(n => n.version_frozen == null && !(n.flag_published == true || n.flag_current == true)));
              fsOp.DB.IKGD_RELATIONs.DeleteAllOnSubmit(fsOp.DB.IKGD_RELATIONs.Where(n => n.version_frozen == null && !(n.flag_published == true || n.flag_current == true)));
              //
              var chg01 = fsOp.DB.GetChangeSet();
              messages.Add("Nodes not frozen removed: {0} [{1} ms]".FormatString(chg01.Deletes.Count, (DateTime.Now - start).TotalMilliseconds));
              fsOp.DB.SubmitChanges();
              //
            }
            if (completeTransaction)
              ts.Committ();
          }
        }
        catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }

        try
        {
          using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(db_timeout))
          {
            using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
            {
              fsOp = _fsOp;
              fsOp.DB.CommandTimeout = db_timeout;
              //
              if (clearFrozenInactiveNodes)
              {
                //
                // cancellazione dei nodi non attivi
                //
                fsOp.DB.IKGD_VNODEs.DeleteAllOnSubmit(fsOp.DB.IKGD_VNODEs.Where(n => n.flag_published == false && n.flag_current == false));
                fsOp.DB.IKGD_INODEs.DeleteAllOnSubmit(fsOp.DB.IKGD_INODEs.Where(n => n.flag_published == false && n.flag_current == false));
                fsOp.DB.IKGD_VDATAs.DeleteAllOnSubmit(fsOp.DB.IKGD_VDATAs.Where(n => n.flag_published == false && n.flag_current == false));
                fsOp.DB.IKGD_PROPERTies.DeleteAllOnSubmit(fsOp.DB.IKGD_PROPERTies.Where(n => n.flag_published == false && n.flag_current == false));
                fsOp.DB.IKGD_RELATIONs.DeleteAllOnSubmit(fsOp.DB.IKGD_RELATIONs.Where(n => n.flag_published == false && n.flag_current == false));
                //
                var chg01a = fsOp.DB.GetChangeSet();
                messages.Add("Nodes inactive removed: {0} [{1} ms]".FormatString(chg01a.Deletes.Count, (DateTime.Now - start).TotalMilliseconds));
                fsOp.DB.SubmitChanges();
              }
            }
            if (completeTransaction)
              ts.Committ();
          }
        }
        catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }

        try
        {
          using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(db_timeout))
          {
            using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
            {
              fsOp = _fsOp;
              fsOp.DB.CommandTimeout = db_timeout;
              //
              // cancellazione dei nodi non attivi
              //
              fsOp.DB.IKGD_VNODEs.Where(r => r.flag_published == false && r.flag_current == true && r.version_frozen != null).ForEach(r => r.version_frozen = null);
              fsOp.DB.IKGD_INODEs.Where(r => r.flag_published == false && r.flag_current == true && r.version_frozen != null).ForEach(r => r.version_frozen = null);
              fsOp.DB.IKGD_VDATAs.Where(r => r.flag_published == false && r.flag_current == true && r.version_frozen != null).ForEach(r => r.version_frozen = null);
              fsOp.DB.IKGD_PROPERTies.Where(r => r.flag_published == false && r.flag_current == true && r.version_frozen != null).ForEach(r => r.version_frozen = null);
              fsOp.DB.IKGD_RELATIONs.Where(r => r.flag_published == false && r.flag_current == true && r.version_frozen != null).ForEach(r => r.version_frozen = null);
              //
              var chg01b = fsOp.DB.GetChangeSet();
              messages.Add("Nodes preview with version_frozen nulled: {0} [{1} ms]".FormatString(chg01b.Updates.Count, (DateTime.Now - start).TotalMilliseconds));
              fsOp.DB.SubmitChanges();
              //
              var props_grp = fsOp.DB.IKGD_PROPERTies.GroupBy(r => new { rnode = r.rnode, version_frozen = r.version_frozen }).Where(g => g.Count() > 1);
              var rels_grp = fsOp.DB.IKGD_RELATIONs.GroupBy(r => new { rnode = r.rnode, version_frozen = r.version_frozen }).Where(g => g.Count() > 1);
              foreach (var grp in props_grp)
              {
                var v_max = grp.Max(r => r.version_date);
                grp.Where(r => r.version_date != v_max).ForEach(r => r.version_date = v_max);
              }
              foreach (var grp in rels_grp)
              {
                var v_max = grp.Max(r => r.version_date);
                grp.Where(r => r.version_date != v_max).ForEach(r => r.version_date = v_max);
              }
              //
              var chg01c = fsOp.DB.GetChangeSet();
              messages.Add("Relationes and Properties with normalized version_date: {0} [{1} ms]".FormatString(chg01c.Updates.Count, (DateTime.Now - start).TotalMilliseconds));
              fsOp.DB.SubmitChanges();
            }
            if (completeTransaction)
              ts.Committ();
          }
        }
        catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }

        try
        {
          //
          // out of date IKGD_FREEZED and IKGD_SNAPSHOT removal
          //
          using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
          {
            messages.AddRange(_fsOp.RemovePendingVoidPublicationRequests(completeTransaction));
          }
        }
        catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }

        try
        {
          using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(db_timeout))
          {
            using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
            {
              fsOp = _fsOp;
              fsOp.DB.CommandTimeout = db_timeout;
              //
              // pulizia snodes non mappati
              //
              fsOp.DB.IKGD_SNODEs.DeleteAllOnSubmit(fsOp.DB.IKGD_SNODEs.Where(n => !n.IKGD_VNODEs.Any()));
              var chg02 = fsOp.DB.GetChangeSet();
              messages.Add("sNodes without vNodes removed: {0} [{1} ms]".FormatString(chg02.Deletes.Count, (DateTime.Now - start).TotalMilliseconds));
              fsOp.DB.SubmitChanges();
            }
            if (completeTransaction)
              ts.Committ();
          }
        }
        catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }

        try
        {
          using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(db_timeout))
          {
            using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
            {
              fsOp = _fsOp;
              fsOp.DB.CommandTimeout = db_timeout;
              //
              // pulizia rnodes non mappati
              //
              try
              {
                //fsOp.DB.IKGD_RNODEs.DeleteAllOnSubmit(fsOp.DB.IKGD_RNODEs.Where(n => !n.IKGD_SNODEs.Any() && !n.IKGD_VNODEs_folder.Any() && !n.IKGD_VNODEs_parent.Any() && !n.IKGD_VNODEs.Any() && !n.IKGD_PROPERTies.Any() && !n.IKGD_RELATIONs.Any() && !n.IKGD_RELATIONs_dst.Any()));
                //var chg03 = fsOp.DB.GetChangeSet();
                //messages.Add("rNodes without dependencies removed: {0} [{1} ms]".FormatString(chg03.Deletes.Count, (DateTime.Now - start).TotalMilliseconds));
                //fsOp.DB.SubmitChanges();
                var rNodes = fsOp.DB.IKGD_RNODEs.Where(n => !n.IKGD_SNODEs.Any() && !n.IKGD_VNODEs_folder.Any() && !n.IKGD_VNODEs_parent.Any() && !n.IKGD_VNODEs.Any() && !n.IKGD_PROPERTies.Any() && !n.IKGD_RELATIONs.Any() && !n.IKGD_RELATIONs_dst.Any()).ToList();
                foreach (var slice in rNodes.Slice(chunkSize))
                {
                  fsOp.DB.IKGD_RNODEs.DeleteAllOnSubmit(slice);
                  // problemi con i vincoli folder parent e vnode
                  var chg03 = fsOp.DB.GetChangeSet();
                  messages.Add("rNodes without dependencies removed: {0} [{1} ms]".FormatString(chg03.Deletes.Count, (DateTime.Now - start).TotalMilliseconds));
                  fsOp.DB.SubmitChanges();
                }
                messages.Add("rNodes without dependencies removed: TOTAL={0} [{1} ms]".FormatString(rNodes.Count, (DateTime.Now - start).TotalMilliseconds));
              }
              catch (Exception ex) { messages.Add("Exception handled while trying to remove rNodes without dependencies: {0}".FormatString(ex.Message)); }
            }
            if (completeTransaction)
              ts.Committ();
          }
        }
        catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }

        try
        {
          using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(db_timeout))
          {
            using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
            {
              fsOp = _fsOp;
              fsOp.DB.CommandTimeout = db_timeout;
              //
              // mStreams senza iNode (non dovrebbero mai essercene per via del cascade)
              //
              //fsOp.DB.IKGD_MSTREAMs.DeleteAllOnSubmit(fsOp.DB.IKGD_MSTREAMs.Where(m => !fsOp.DB.IKGD_INODEs.Any(n => n.version == m.inode)));
              //var chg04 = fsOp.DB.GetChangeSet();
              //messages.Add("mStreams without iNode removed: {0}".FormatString(chg04.Deletes.Count));
              //fsOp.DB.SubmitChanges();

              // traducendo questa query con linqpad andiamo ad eseguire direttamente il codice SQL di cancellazione
              int rowsAll = fsOp.DB.ExecuteCommand(@"SELECT COUNT(*) FROM [IKGD_MSTREAM] AS [t0] WHERE NOT (EXISTS(SELECT NULL AS [EMPTY] FROM [IKGD_INODE] AS [t1] WHERE [t1].[version] = [t0].[inode]))");
              int rows = fsOp.DB.ExecuteCommand(@"DELETE [IKGD_MSTREAM] FROM [IKGD_MSTREAM] AS [t0] WHERE NOT (EXISTS(SELECT NULL AS [EMPTY] FROM [IKGD_INODE] AS [t1] WHERE [t1].[version] = [t0].[inode]))");
              messages.Add("mStreams without iNode removed: {0} expected={1} [{2} ms]".FormatString(rows, rowsAll, (DateTime.Now - start).TotalMilliseconds));
            }
            if (completeTransaction)
              ts.Committ();
          }
        }
        catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }

        try
        {
          using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
          {
            fsOp = _fsOp;
            //
            // streams senza iNode o mStream
            //
            // questa query scoppia con out of memory perche' deve leggere interamente TUTTI i record da cancellare!
            //fsOp.DB.IKGD_STREAMs.DeleteAllOnSubmit(
            //  from stream in fsOp.DB.IKGD_STREAMs
            //  join ms in fsOp.DB.IKGD_MSTREAMs on stream.id equals ms.stream into mstreams
            //  join ino in fsOp.DB.IKGD_INODEs on stream.inode equals ino.version into inodes
            //  where !mstreams.Any() && !inodes.Any()
            //  select stream);
            //var chg05 = fsOp.DB.GetChangeSet();
            //fsOp.DB.SubmitChanges();
            //
            // traducendo questa query con linqpad andiamo ad eseguire direttamente il codice SQL di cancellazione
            //var streamIds =
            //  (from stream in fsOp.DB.IKGD_STREAMs
            //   join ms in fsOp.DB.IKGD_MSTREAMs on stream.id equals ms.stream into mstreams
            //   join ino in fsOp.DB.IKGD_INODEs on stream.inode equals ino.version into inodes
            //   where !mstreams.Any() && !inodes.Any()
            //   select stream.id).Distinct().ToList();
            fsOp.DB.CommandTimeout = db_timeout * 2;
            int recordCount = fsOp.DB.GetScalarValueSimple<int>(@"SELECT COUNT(*) FROM [IKGD_STREAM] WHERE [id] IN (SELECT DISTINCT [t0].[id] FROM [IKGD_STREAM] AS [t0] WHERE (NOT (EXISTS(SELECT NULL AS [EMPTY] FROM [IKGD_MSTREAM] AS [t1] WHERE [t0].[id] = [t1].[stream]))) AND (NOT (EXISTS(SELECT NULL AS [EMPTY] FROM [IKGD_INODE] AS [t2] WHERE [t0].[inode] = ([t2].[version])))))");
            messages.Add("streams without inode or mstream to be removed: {0} [{1} ms]".FormatString(recordCount, (DateTime.Now - start).TotalMilliseconds));
            for (int i = 0; i < recordCount; i += chunkSize)
            {
              using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(db_timeout))
              {
                //var cmd = fsOp.DB.Connection.CreateCommand();
                //cmd.CommandTimeout = fsOp.DB.CommandTimeout;
                //cmd.CommandText = @"DELETE FROM [IKGD_STREAM] WHERE [id] IN (SELECT DISTINCT TOP {0} [t0].[id] FROM [IKGD_STREAM] AS [t0] WHERE (NOT (EXISTS(SELECT NULL AS [EMPTY] FROM [IKGD_MSTREAM] AS [t1] WHERE [t0].[id] = [t1].[stream]))) AND (NOT (EXISTS(SELECT NULL AS [EMPTY] FROM [IKGD_INODE] AS [t2] WHERE [t0].[inode] = ([t2].[version])))))";
                //var param01 = cmd.CreateParameter();
                //param01.Value = chunkSize;
                //cmd.Parameters.Add(param01);
                //int rowsAll2 = (int)cmd.ExecuteScalar();
                //
                // con questa sintassi si incasina il parser dell'sql!
                //int rowsAll = fsOp.DB.ExecuteCommand(@"DELETE FROM [IKGD_STREAM] WHERE [id] IN (SELECT DISTINCT TOP {0} [t0].[id] FROM [IKGD_STREAM] AS [t0] WHERE (NOT (EXISTS(SELECT NULL AS [EMPTY] FROM [IKGD_MSTREAM] AS [t1] WHERE [t0].[id] = [t1].[stream]))) AND (NOT (EXISTS(SELECT NULL AS [EMPTY] FROM [IKGD_INODE] AS [t2] WHERE [t0].[inode] = ([t2].[version])))))", chunkSize);
                int rowsAll = fsOp.DB.ExecuteCommand("DELETE FROM [IKGD_STREAM] WHERE [id] IN (SELECT DISTINCT TOP {0} [t0].[id] FROM [IKGD_STREAM] AS [t0] WHERE (NOT (EXISTS(SELECT NULL AS [EMPTY] FROM [IKGD_MSTREAM] AS [t1] WHERE [t0].[id] = [t1].[stream]))) AND (NOT (EXISTS(SELECT NULL AS [EMPTY] FROM [IKGD_INODE] AS [t2] WHERE [t0].[inode] = ([t2].[version])))))".FormatString(chunkSize));
                messages.Add("streams without inode or mstream removed: {0} [{1} ms]".FormatString(rowsAll, (DateTime.Now - start).TotalMilliseconds));
                //
                ts.Committ();
              }
            }
          }
        }
        catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }

        try
        {
          using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(db_timeout))
          {
            using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
            {
              fsOp = _fsOp;
              fsOp.DB.CommandTimeout = db_timeout;
              int rows1 = fsOp.DB.ExecuteCommand(@"UPDATE IKGD_STREAM SET [source]=NULL WHERE ([source]='')");
              int rows3 = fsOp.DB.ExecuteCommand(@"DELETE FROM IKGD_PROPERTY WHERE [name]={0} AND [attributeId] IS NULL", IKGD_Constants.IKCAT_TagPropertyName);
              fsOp.DB.IKGD_PROPERTies.DeleteAllOnSubmit(fsOp.DB.IKGD_PROPERTies.Where(r => r.name == IKGD_Constants.IKCAT_TagPropertyName).Where(r => r.IKCAT_Attribute == null));
              var chg03 = fsOp.DB.GetChangeSet();
              messages.Add("streams with source='' normalized: {0}".FormatString(rows1));
              messages.Add("IKGD_PROPERTY name='IKCAT_Tag' with attributeId == NULL: {0} [{1} ms]".FormatString(rows3, (DateTime.Now - start).TotalMilliseconds));
              messages.Add("IKGD_PROPERTY name='IKCAT_Tag' with attributeId missing map: {0} [{1} ms]".FormatString(chg03.Deletes.Count, (DateTime.Now - start).TotalMilliseconds));
              fsOp.DB.SubmitChanges();
            }
            if (completeTransaction)
              ts.Committ();
          }
        }
        catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }

        try
        {
          using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(db_timeout))
          {
            using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
            {
              fsOp = _fsOp;
              fsOp.DB.CommandTimeout = db_timeout;
              //
              // normalizzazione dei nomi files registrati in iNode in forma non canonica
              //
              try
              {
                var iNodes = fsOp.DB.IKGD_INODEs.Where(n => n.filename.Contains(":") || n.filename.Contains("\\") || n.filename.Contains("/")).ToList();
                foreach (var iNode in iNodes)
                {
                  string filename = Utility.PathGetFileNameSanitized(iNode.filename);
                  if (!string.IsNullOrEmpty(filename))
                  {
                    filename = filename.Replace("/", "-");
                    if (iNode.filename != filename)
                      iNode.filename = filename;
                  }
                }
                var chg03 = fsOp.DB.GetChangeSet();
                messages.Add("iNodes: filenames normalizzati {0} [{1} ms]".FormatString(chg03.Updates.Count, (DateTime.Now - start).TotalMilliseconds));
                fsOp.DB.SubmitChanges();
              }
              catch (Exception ex) { messages.Add("Exception handled while trying to normalize iNodes filenames: {0}".FormatString(ex.Message)); }
            }
            if (completeTransaction)
              ts.Committ();
          }
        }
        catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }

        try
        {
          using (IKGD_ExternalVFS_Support extFS = new IKGD_ExternalVFS_Support())
          {
            if (extFS.IsActive())
            {
              messages.AddRange(extFS.ClearUnmappedExternalResources(true, false));
            }
          }
        }
        catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }

        // da eliminare dopo aver tolto il field "data" da IKGD_VDATA, e' normale che generi eccezioni
        //try
        //{
        //  using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
        //  {
        //    using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
        //    {
        //      fsOp = _fsOp;
        //      fsOp.DB.CommandTimeout = 3600;
        //      int rows2 = fsOp.DB.ExecuteCommand(@"UPDATE IKGD_VDATA SET [data]=NULL WHERE [data] IS NOT NULL");
        //      messages.Add("IKGD_VDATA with data filled but not used: {0}".FormatString(rows2));
        //    }
        //    if (completeTransaction)
        //      ts.Committ();
        //  }
        //}
        //catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }

      }
      catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }
      return messages;
    }


    public List<string> ClearQueues(bool completeTransaction)
    {
      List<string> messages = new List<string>();
      try
      {
        using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
        {
          using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
          {
            fsOp = _fsOp;
            fsOp.DB.CommandTimeout = 3600;
            //
            int rowsQueue = fsOp.DB.ExecuteCommand("DELETE FROM [IKGD_QueueMeta]");
            messages.Add("items removed from queued events: {0}".FormatString(rowsQueue));
            //
            int rowsElmah = fsOp.DB.ExecuteCommand("TRUNCATE TABLE [ELMAH_Error]");
            messages.Add("items removed from ELMAH_Error: {0}".FormatString(rowsElmah));
            //
            var searchRecords = fsOp.DB.IKGD_SEARCHes.GroupBy(r => new { r.server, r.status }).SelectMany(g => g.OrderByDescending(r => r.id).Skip(1)).ToList();
            fsOp.DB.IKGD_SEARCHes.DeleteAllOnSubmit(searchRecords);
            messages.Add("removed older search records: {0}".FormatString(rowsQueue));
            //
            fsOp.DB.SubmitChanges();
          }
          if (completeTransaction)
            ts.Committ();
        }
        //
        using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
        {
          using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
          {
            fsOp = _fsOp;
            fsOp.DB.CommandTimeout = 3600;
            //
            try
            {
              int? HitLogMax = null;
              try { HitLogMax = fsOp.DB.IKG_HITLOGs.OrderByDescending(r => r.id).Skip(Utility.TryParse<int>(IKGD_Config.AppSettings["IKG_HITLOG_MaxSize"], 500000)).Max(r => r.id); }
              catch { }
              if (HitLogMax != null)
              {
                int rowsTransferred = fsOp.DB.ExecuteCommand("INSERT INTO IKG_HITLOG_ARCHIVE SELECT * FROM IKG_HITLOG WHERE id < {0}".FormatString(HitLogMax.Value));
                int rowsDeleted = fsOp.DB.ExecuteCommand("DELETE FROM IKG_HITLOG WHERE id < {0}".FormatString(HitLogMax.Value));
                messages.Add("records transferred to IKG_HITLOG_ARCHIVE: {0}".FormatString(rowsTransferred));
                messages.Add("records removed from IKG_HITLOG: {0}".FormatString(rowsDeleted));
              }
              //
              int HitLogMaxDays = Utility.TryParse<int>(IKGD_Config.AppSettings["IKG_HITLOG_MaxDays"], 180);
              int HitLogMaxHitsPerUser = Utility.TryParse<int>(IKGD_Config.AppSettings["IKG_HITLOG_MaxHitsPerUser"], 100);
              List<int> recordIds = new List<int>();
              DateTime minDate = DateTime.Now.AddDays(-HitLogMaxDays).Date;
              recordIds.AddRange(fsOp.DB.IKG_HITLOGs.Where(r => r.ts < minDate).Select(r => r.id));
              recordIds.AddRange(fsOp.DB.IKG_HITLOGs.GroupBy(r => r.wID).SelectMany(g => g.OrderByDescending(r => r.id).Skip(HitLogMaxHitsPerUser).Select(r => r.id)));
              recordIds = recordIds.Distinct().ToList();
              foreach (var slice in recordIds.Slice(5000))
              {
                int rowsTransferred2 = fsOp.DB.ExecuteCommand("INSERT INTO IKG_HITLOG_ARCHIVE SELECT * FROM IKG_HITLOG WHERE id IN ({0})".FormatString(Utility.Implode(slice, ",")));
                int rowsDeleted2 = fsOp.DB.ExecuteCommand("DELETE FROM IKG_HITLOG WHERE id IN ({0})".FormatString(Utility.Implode(slice, ",")));
              }
              messages.Add("records transferred to IKG_HITLOG_ARCHIVE: {0}  [with user and date limits]".FormatString(recordIds.Count));
              messages.Add("records removed from IKG_HITLOG: {0}  [with user and date limits]".FormatString(recordIds.Count));
            }
            catch { }
            //
            fsOp.DB.SubmitChanges();
          }
          if (completeTransaction)
            ts.Committ();
        }
        //
        using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
        {
          using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
          {
            fsOp = _fsOp;
            fsOp.DB.CommandTimeout = 3600;
            //
            try
            {
              int rowsDeleted = fsOp.DB.ExecuteCommand("DELETE FROM LazyLoginMapper WHERE UserId='00000000-0000-0000-0000-000000000000'");
              messages.Add("records generated by crawlers removed from LazyLoginMapper: {0}".FormatString(rowsDeleted));
            }
            catch { }
            //
            fsOp.DB.SubmitChanges();
          }
          if (completeTransaction)
            ts.Committ();
        }
      }
      catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }
      return messages;
    }


    public List<string> ShrinkDB(bool completeTransaction)
    {
      List<string> messages = new List<string>();
      try
      {
        using (FS_Operations _fsOp = new FS_Operations())
        {
          _fsOp.DB.CommandTimeout = 3600;
          _fsOp.DB.ExecuteCommand(string.Format("DBCC SHRINKDATABASE('{0}')", _fsOp.DB.Connection.Database));
        }
      }
      catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }
      return messages;
    }


    public void VFSPublishNode(IKGD_XNODE node, int version_frozen)
    {
      if (node.flag_current && node.flag_deleted)
        node.flag_current = false;
      if (node.flag_deleted)
        node.flag_deleted = false;
      if (node.flag_published && !node.flag_current)
        node.flag_published = false;
      if (!node.flag_published && node.flag_current)
        node.flag_published = true;
      if (node.flag_published && node.version_frozen == null)
        node.version_frozen = version_frozen;
    }


    public List<string> VFSPublishAll(bool completeTransaction)
    {
      List<string> messages = new List<string>();
      try
      {
        int version_frozen = 0;
        using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
        {
          fsOp = _fsOp;
          fsOp.DB.CommandTimeout = 3600;
          using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
          {
            //
            var inactive_versions = fsOp.DB.IKGD_SNAPSHOTs.Select(r => r.version_frozen).Except(fsOp.DB.IKGD_VNODEs.Select(n => n.version_frozen).Union(fsOp.DB.IKGD_INODEs.Select(n => n.version_frozen)).Union(fsOp.DB.IKGD_VDATAs.Select(n => n.version_frozen)).Distinct().Where(v => v != null).Select(v => v.Value)).ToList();
            foreach (var _inactive_versions in inactive_versions.Slice(500))
            {
              var _inactive_versions_slice = _inactive_versions.ToList();
              fsOp.DB.IKGD_SNAPSHOTs.DeleteAllOnSubmit(fsOp.DB.IKGD_SNAPSHOTs.Where(r => _inactive_versions_slice.Contains(r.version_frozen)));
            }
            /*
            //TODO: verificare
            var active_versions = fsOp.DB.IKGD_VNODEs.Select(n => n.version_frozen).Union(fsOp.DB.IKGD_INODEs.Select(n => n.version_frozen)).Union(fsOp.DB.IKGD_VDATAs.Select(n => n.version_frozen)).Distinct().ToList();
            foreach (var _active_versions in active_versions.Slice(500))
            {
              var _active_versions_slice = _active_versions.ToList();
              fsOp.DB.IKGD_SNAPSHOTs.DeleteAllOnSubmit(fsOp.DB.IKGD_SNAPSHOTs.Where(r => _active_versions_slice.Contains(r.version_frozen)));
            }
            */
            //
            // ottiene un nuovo ID per il freeze
            //
            IKGD_SNAPSHOT freeze = new IKGD_SNAPSHOT { date_frozen = DateTime.Now, flag_published = false, flag_rejected = false, username = fsOp.CurrentUser, snode_root = 1, snode_folder = 1, affected = 0, name = "root", path = "/" };
            fsOp.DB.IKGD_SNAPSHOTs.InsertOnSubmit(freeze);
            //
            int res01 = fsOp.DB.ExecuteCommand("DELETE FROM [IKGD_FREEZED]");
            //
            //var chg01 = DB.GetChangeSet();
            fsOp.DB.SubmitChanges();
            version_frozen = freeze.version_frozen;
            if (completeTransaction)
              ts.Committ();
          }
          //
          if (version_frozen == 0)
          {
            messages.Add("version_frozen invalid: {0}".FormatString(version_frozen));
            return messages;
          }
          ChangeSet chg = null;
          int chunksize = 250;
          //
          do
          {
            using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
            {
              fsOp.DB.IKGD_VDATAs.Where(n => (n.flag_published == true || n.flag_current == true || n.flag_deleted == true) && (n.flag_published == false || n.flag_current == false || n.flag_deleted == true)).Take(chunksize).ForEach(n => VFSPublishNode(n, version_frozen));
              chg = fsOp.DB.GetChangeSet();
              messages.Add("IKGD_VDATAs published : {0} -> normalized".FormatString(chg.Updates.Count));
              fsOp.DB.SubmitChanges();
              ts.Committ();
            }
          }
          while (chg.Updates.Count > 0);

          do
          {
            using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
            {
              fsOp.DB.IKGD_VNODEs.Where(n => (n.flag_published == true || n.flag_current == true || n.flag_deleted == true) && (n.flag_published == false || n.flag_current == false || n.flag_deleted == true)).Take(chunksize).ForEach(n => VFSPublishNode(n, version_frozen));
              chg = fsOp.DB.GetChangeSet();
              messages.Add("IKGD_VNODEs published : {0} -> normalized".FormatString(chg.Updates.Count));
              fsOp.DB.SubmitChanges();
              ts.Committ();
            }
          }
          while (chg.Updates.Count > 0);

          do
          {
            using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
            {
              fsOp.DB.IKGD_INODEs.Where(n => (n.flag_published == true || n.flag_current == true || n.flag_deleted == true) && (n.flag_published == false || n.flag_current == false || n.flag_deleted == true)).Take(chunksize).ForEach(n => VFSPublishNode(n, version_frozen));
              chg = fsOp.DB.GetChangeSet();
              messages.Add("IKGD_INODEs published : {0} -> normalized".FormatString(chg.Updates.Count));
              fsOp.DB.SubmitChanges();
              ts.Committ();
            }
          }
          while (chg.Updates.Count > 0);

          do
          {
            using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
            {
              fsOp.DB.IKGD_PROPERTies.Where(n => (n.flag_published == true || n.flag_current == true || n.flag_deleted == true) && (n.flag_published == false || n.flag_current == false || n.flag_deleted == true)).Take(chunksize).ForEach(n => VFSPublishNode(n, version_frozen));
              chg = fsOp.DB.GetChangeSet();
              messages.Add("IKGD_PROPERTies published : {0} -> normalized".FormatString(chg.Updates.Count));
              fsOp.DB.SubmitChanges();
              ts.Committ();
            }
          }
          while (chg.Updates.Count > 0);

          do
          {
            using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
            {
              fsOp.DB.IKGD_RELATIONs.Where(n => (n.flag_published == true || n.flag_current == true || n.flag_deleted == true) && (n.flag_published == false || n.flag_current == false || n.flag_deleted == true)).Take(chunksize).ForEach(n => VFSPublishNode(n, version_frozen));
              chg = fsOp.DB.GetChangeSet();
              messages.Add("IKGD_RELATIONs published : {0} -> normalized".FormatString(chg.Updates.Count));
              fsOp.DB.SubmitChanges();
              ts.Committ();
            }
          }
          while (chg.Updates.Count > 0);

        }
      }
      catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }
      return messages;
    }


    //
    // supporto per il rebuild di IKGD_VDATA_KEYVALUE
    //
    public List<string> VFSRebuild_vDataKeyValues() { return VFSRebuild_vDataKeyValues(null, null, null); }
    public List<string> VFSRebuild_vDataKeyValues(bool? process_published, bool? process_preview, bool? process_freezed)
    {
      List<string> messages = new List<string>();
      try
      {
        using (FS_Operations _fsOp = new FS_Operations(-1, false, true, true))
        {
          fsOp = _fsOp;
          fsOp.DB.CommandTimeout = 3600;
          //
          Expression<Func<IKGD_VDATA, bool>> filter = PredicateBuilder.False<IKGD_VDATA>();
          if (process_published.GetValueOrDefault(true))
          {
            filter = filter.Or(n => n.flag_published == true);
          }
          if (process_preview.GetValueOrDefault(true))
          {
            filter = filter.Or(n => n.flag_current == true);
          }
          if (process_freezed.GetValueOrDefault(true))
          {
            filter = filter.Or(n => n.version_frozen != null);
          }
          //
          var vDatas = fsOp.DB.IKGD_VDATAs.Where(n => n.flag_inactive == false).Where(filter.Compile());
          int updates = fsOp.Update_vDataKeyValues(vDatas);
          //
          messages.Add("Processed {0} nodes.".FormatString(updates));
          //
        }
      }
      catch (Exception ex) { messages.Add("Exception: {0}".FormatString(ex.Message)); }
      return messages;
    }


  }  //class 


}