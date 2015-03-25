using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
using acadApp = Autodesk.AutoCAD.ApplicationServices.Application;
//using Autodesk.AutoCAD.Colors;
//using Autodesk.AutoCAD;
//using Autodesk.AutoCAD.Internal;
//using Autodesk.AutoCAD.Interop;
//using Autodesk.AutoCAD.Interop.Common;
//using Autodesk.AutoCAD.Windows;
//using System.Linq;
//using System.Text;
//using System.Windows.Forms;
//using System.Drawing;
//using System.Collections.Generic;
//using System.Collections.Specialized;
//using System.Runtime;
//using System.Runtime.InteropServices;
using System;
//using System.Reflection;
//using System.Collections;
//using System.Reflection;
//using System.IO;

namespace HSKDIProject
{
    public class Commands
    {
        class MLeaderJig : EntityJig
        {
            Point3dCollection m_pts;
            Point3d m_tempPoint;            
            int m_leaderIndex;
            int m_leaderLineIndex;

            public MLeaderJig(ObjectId mlStyle, MText mtext) : base(new MLeader())
            {
                // Store the string passed in 
                //m_contents = contents;
                // Create a point collection to store our vertices
                m_pts = new Point3dCollection();
                // Create mleader and set defaults
                MLeader ml = Entity as MLeader;
                ml.SetDatabaseDefaults();
                                
                // Set up the MText contents
                ml.ContentType = ContentType.MTextContent;
                ml.MText = mtext;
                //ml.MText.TextHeight = textHeight;
                ml.MLeaderStyle = mlStyle;
                
                //ml.TextAlignmentType = TextAlignmentType.LeftAlignment;
                //ml.TextAttachmentType = TextAttachmentType.AttachmentMiddleOfTop;

                // Set the frame and landing properties
                //ml.EnableDogleg = true;
                //ml.EnableFrameText = true;
                //ml.EnableLanding = true;

                // Reduce the standard landing gap
                //ml.LandingGap = 0.05;

                // Add a leader, but not a leader line (for now)
                m_leaderIndex = ml.AddLeader();
                m_leaderLineIndex = -1;
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                JigPromptPointOptions opts = new JigPromptPointOptions();

                // Not all options accept null response
                opts.UserInputControls = (UserInputControls.Accept3dCoordinates
                                         | UserInputControls.NoNegativeResponseAccepted);

                // Get the first point
                if (m_pts.Count == 0)
                {
                    opts.UserInputControls |= UserInputControls.NullResponseAccepted;
                    opts.Message = "\nStart point of multileader arrow: ";
                    opts.UseBasePoint = false;
                }
                // And the second
                else if (m_pts.Count == 1)
                {
                    opts.BasePoint = m_pts[m_pts.Count - 1];
                    opts.UseBasePoint = true;
                    opts.Message = "\nSpecify multileader vertex: ";
                }
                // And subsequent points
                else if (m_pts.Count > 1)
                {
                    opts.UserInputControls |= UserInputControls.NullResponseAccepted;
                    opts.BasePoint = m_pts[m_pts.Count - 1];
                    opts.UseBasePoint = true;
                    //opts.Message ="\nSpecify additional multileader vertex: ";
                    opts.SetMessageAndKeywords("\nSpecify additional multileader vertex or [End]: ", "End");                    
                }
                else // Should never happen
                    return SamplerStatus.Cancel;

                PromptPointResult res = prompts.AcquirePoint(opts);

                //if (res.Status == PromptStatus.Keyword)
                //{
                //    if (res.StringResult == "End")
                //    {
                //        return SamplerStatus.Cancel;
                //    }
                //}

                if (m_tempPoint == res.Value)
                {
                    return SamplerStatus.NoChange;
                }
                else if (res.Status == PromptStatus.OK)
                {
                    m_tempPoint = res.Value;
                    return SamplerStatus.OK;
                }
                return SamplerStatus.Cancel;
            }

            protected override bool Update()
            {
                try
                {
                    if (m_pts.Count > 0)
                    {
                        // Set the last vertex to the new value
                        MLeader ml = Entity as MLeader;
                        ml.SetLastVertex(m_leaderLineIndex, m_tempPoint);

                        // Adjust the text location
                        Vector3d dogvec = ml.GetDogleg(m_leaderIndex);
                        double doglen = ml.DoglegLength;
                        double landgap = ml.LandingGap;
                        ml.TextLocation = m_tempPoint + ((doglen + landgap) * dogvec);
                    }
                }
                catch (System.Exception ex)
                {
                    Document doc = acadApp.DocumentManager.MdiActiveDocument;
                    doc.Editor.WriteMessage("\nException: " + ex.Message);
                    return false;
                }
                return true;
            }

            public void AddVertex()
            {
                MLeader ml = Entity as MLeader;

                // For the first point...
                if (m_pts.Count == 0)
                {
                    // Add a leader line
                    m_leaderLineIndex = ml.AddLeaderLine(m_leaderIndex);

                    // And a start vertex
                    ml.AddFirstVertex(m_leaderLineIndex, m_tempPoint);

                    // Add a second vertex that will be set
                    // within the jig
                    ml.AddLastVertex(m_leaderLineIndex, new Point3d(0, 0, 0));
                }
                else
                {
                    // For subsequent points,
                    // just add a vertex
                    ml.AddLastVertex(m_leaderLineIndex, m_tempPoint);
                }

                // Reset the attachment point, otherwise
                // it seems to get forgotten
                ml.TextAttachmentType = TextAttachmentType.AttachmentMiddle;

                // Add the latest point to our history
                m_pts.Add(m_tempPoint);
            }

            public void RemoveLastVertex()
            {
                // We don't need to actually remove
                // the vertex, just reset it
                MLeader ml = Entity as MLeader;
                if (m_pts.Count >= 1)
                {
                    Vector3d dogvec = ml.GetDogleg(m_leaderIndex);
                    double doglen = ml.DoglegLength;
                    double landgap = ml.LandingGap;
                    ml.SetDogleg(m_leaderIndex, dogvec);
                    ml.TextLocation = m_pts[m_pts.Count - 1] + ((doglen + landgap) * dogvec);
                    if (ml.LandingGap > 0) ml.RemoveLastVertex(0);
                    
                }
            }

            public Entity GetEntity()
            {
                return Entity;
            }
        }

        [CommandMethod("Txt2MLeader")]
        public void Txt2MLeaderJig()
        {
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            PromptEntityOptions peo = new PromptEntityOptions("\nSelect Text/MText Object");
            peo.AllowNone = false;
            peo.AllowObjectOnLockedLayer = true;
            peo.SetRejectMessage("\nBad Selection. Select Text/MText Object");
            peo.AddAllowedClass(typeof(MText), false);
            peo.AddAllowedClass(typeof(DBText), false);            

            PromptEntityResult per = ed.GetEntity(peo);

            if (per.Status == PromptStatus.OK)
            {
                Transaction tr = doc.TransactionManager.StartTransaction();
                using (tr)
                {
                    string txtString = null;                    
                    double txtHeight = .08;
                    string styleName = "HSKDI-Lasso";
                    string textStyleName = "HSKDI-Text";
                    DBDictionary mlStyles = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead);
                    ObjectId mlStyleId = ObjectId.Null;

                    if (!mlStyles.Contains(styleName))
                    {
                        mlStyleId = AddMultileaderStyle(styleName, "_DOTBLANK", .02, 0, false, .01, .01, LeaderType.SplineLeader, textStyleName, txtHeight, AnnotativeStates.False);                        
                    }
                    else
                    {
                        mlStyleId = mlStyles.GetAt(styleName);
                    }
                    db.MLeaderstyle = mlStyleId;

                                        
                    MText mt = new MText();
                    TextStyleTable txtStyles = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    mt.TextStyleId = txtStyles[textStyleName];                    
                    mt.TextHeight = txtHeight;
                    Entity ent = (Entity)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    string entType = ent.GetType().ToString().Split('.')[ent.GetType().ToString().Split('.').Length - 1];

                    switch (entType)
                    {
                        case "MText":
                            mt = (MText)ent;                            
                            txtString = mt.getMTextWithFieldCodes();
                            txtHeight = mt.TextHeight;
                            break;
                        case "DBText":                            
                            DBText dbt = (DBText)ent;
                            txtString = dbt.TextString;
                            txtHeight = dbt.Height;
                            break;                            
                    }
                    if (txtString != null)
                    {                            
                        // Create MleaderJig
                        MLeaderJig jig = new MLeaderJig(mlStyleId, mt);

                        // Loop to set vertices
                        bool bSuccess = true, bComplete = false;
                        while (bSuccess && !bComplete)
                        {
                            PromptResult dragres = ed.Drag(jig);
                            bSuccess = (dragres.Status == PromptStatus.OK);
                            if (bSuccess) jig.AddVertex();
                            bComplete = (dragres.Status == PromptStatus.None);
                            if (bComplete) jig.RemoveLastVertex();                                
                        }

                        if (bComplete)
                        {
                            // Append entity
                            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead, false);
                            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite, false);
                            btr.AppendEntity(jig.GetEntity());
                            ent.UpgradeOpen();
                            ent.Erase();
                            tr.AddNewlyCreatedDBObject(jig.GetEntity(), true);                            
                            tr.Commit();
                        }
                    }

                    else
                    {
                        ed.WriteMessage("\nBad Selection. Aborting.");
                        tr.Commit();
                    }
                }
            }
        }

        private static ObjectId AddMultileaderStyle(string styleName, string arrowStyle, double arrowSize, double dogLeg, bool textFrame, double landing, double landingGap, LeaderType thisLeaderType, string textStyleName, double textHeightpt, AnnotativeStates annotatitionType)
        {
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            ObjectId mlStyle = ObjectId.Null;

            MLeaderStyle newStyle = new MLeaderStyle();

             Transaction tr = doc.TransactionManager.StartTransaction();
             using (tr)
             {

                 BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                 
                 if (!bt.Has(arrowStyle)) acadApp.SetSystemVariable("DIMBLK", "_DotBlank");

                 newStyle.Annotative = annotatitionType;
                 //double multiplyer = 0;
                 if (annotatitionType != AnnotativeStates.True)
                 {
                     newStyle.Scale = HSKDICommon.Commands.getdimscale();
                 }
                 

                 newStyle.ArrowSymbolId = bt[arrowStyle];
                 newStyle.ArrowSize = arrowSize;
                 newStyle.ContentType = ContentType.MTextContent;                 
                 newStyle.EnableFrameText = textFrame;
                 newStyle.LandingGap = landing;
                 newStyle.EnableLanding = newStyle.LandingGap > 0 ? true : false;
                 newStyle.DoglegLength = dogLeg;
                 newStyle.EnableDogleg = newStyle.DoglegLength > 0 ? true : false;
                 newStyle.ExtendLeaderToText = true;                 
                 newStyle.LeaderLineType = thisLeaderType;
                 newStyle.MaxLeaderSegmentsPoints = 2;
                 newStyle.TextHeight = textHeightpt;

                 newStyle.TextAttachmentDirection = TextAttachmentDirection.AttachmentHorizontal;                 
                 newStyle.SetTextAttachmentType(TextAttachmentType.AttachmentMiddleOfTop, LeaderDirectionType.LeftLeader);
                 newStyle.SetTextAttachmentType(TextAttachmentType.AttachmentMiddleOfTop, LeaderDirectionType.RightLeader);
                 
                 newStyle.TextAlignAlwaysLeft = true;
                 
                 newStyle.TextAlignmentType = TextAlignmentType.RightAlignment;
                 newStyle.TextAngleType = TextAngleType.HorizontalAngle;
                 
                 

                 TextStyleTable txtStyles = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                 try
                 {
                     newStyle.TextStyleId = txtStyles[textStyleName];
                 }
                 catch { }

                 mlStyle = newStyle.PostMLeaderStyleToDb(db, styleName);
                 tr.AddNewlyCreatedDBObject(newStyle, true);
                 tr.Commit();                 
             }
             return mlStyle;
        }
        

    }    
}