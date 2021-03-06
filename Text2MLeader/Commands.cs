using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
using acadApp = Autodesk.AutoCAD.ApplicationServices.Application;


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
                // Create a point collection to store our vertices
                m_pts = new Point3dCollection();
                
                // Create mleader and set defaults
                MLeader ml = Entity as MLeader;
                ml.SetDatabaseDefaults();
                                
                // Set up the MText contents
                ml.ContentType = ContentType.MTextContent;
                ml.MText = mtext;
                ml.MLeaderStyle = mlStyle;                
                m_leaderIndex = ml.AddLeader();
                m_leaderLineIndex = -1;
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                JigPromptPointOptions opts = new JigPromptPointOptions()
                {

                    // Not all options accept null response
                    UserInputControls = (UserInputControls.Accept3dCoordinates
                                         | UserInputControls.NoNegativeResponseAccepted)
                };

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
                    opts.SetMessageAndKeywords("\nSpecify additional multileader vertex or [End]: ", "End");                    
                }
                else // Should never happen
                    return SamplerStatus.Cancel;

                PromptPointResult res = prompts.AcquirePoint(opts);

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

        [CommandMethod("Lasso")]
        public void LassoText()
        {
            Document doc = acadApp.DocumentManager.MdiActiveDocument;            
            Database db = doc.Database;
            ObjectId mlStyleId = ObjectId.Null;

            double txtHeight = .08;
            string styleName = "HSKDI-Lasso";            
            string textStyleName = "HSKDI-Text";
            double arrowSize = .02;
            string arrowHead = "_DOTBLANK";
            double dogLeg = 0;
            bool textFrame = false;
            double landing = .01;
            double landingGap = landing;
            LeaderType leaderType = LeaderType.SplineLeader;
            AnnotativeStates annotateiveState = AnnotativeStates.False;
            TextAttachmentType textAttachType = TextAttachmentType.AttachmentMiddleOfTop;
            
            Transaction tr = doc.TransactionManager.StartTransaction();
            using (tr)
            {
                DBDictionary mlStyles = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead);
                
                if (!mlStyles.Contains(styleName))
                {
                    mlStyleId = AddMultileaderStyle(styleName,
                                                    arrowHead,
                                                    arrowSize,
                                                    dogLeg,
                                                    textFrame,
                                                    landing,
                                                    landingGap,
                                                    leaderType,
                                                    textStyleName,
                                                    txtHeight,
                                                    annotateiveState,
                                                    textAttachType);
                }
                else
                {
                    mlStyleId = mlStyles.GetAt(styleName);
                }
                db.MLeaderstyle = mlStyleId;
                tr.Commit();
            }            
            Text2Mleader(styleName, mlStyleId);
        }

        [CommandMethod("Txt2Mleader")]
        public void Text2Mleader()
        {            
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            Transaction tr = doc.TransactionManager.StartTransaction();
                using (tr)
                {

                    DBDictionary mlStyleDict = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead);
                    ObjectId mlStyleId = (ObjectId)mlStyleDict.GetAt((string)acadApp.GetSystemVariable("cmleaderstyle"));
                    MLeaderStyle mlStyle = (MLeaderStyle)tr.GetObject(mlStyleId, OpenMode.ForRead);
                    Text2Mleader(mlStyle.Name, mlStyleId);
                    tr.Commit();
                    
            
            //PromptEntityOptions peo = new PromptEntityOptions("\nSelect Text/MText Object to add in current Mleader Style");
            //peo.AllowNone = false;
            //peo.AllowObjectOnLockedLayer = true;            
            //peo.SetRejectMessage("\nBad Selection. Select Text/MText Object: ");
            //peo.AddAllowedClass(typeof(MText), false);
            //peo.AddAllowedClass(typeof(DBText), false);      

            //PromptEntityResult per = ed.GetEntity(peo);

            ////while?
            //if (per.Status == PromptStatus.OK)
            //{
                
                    //Entity ent = (Entity)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    //string entType = ent.GetType().ToString().Split('.')[ent.GetType().ToString().Split('.').Length - 1];

                    //switch (entType)
                    //{
                    //    case "MText":
                    //        MText tempMt = (MText)ent;
                    //        txtString = tempMt.getMTextWithFieldCodes();
                    //        break;
                    //    case "DBText":
                    //        DBText dbt = (DBText)ent;
                    //        txtString = dbt.TextString;
                    //        break;
                    //}
                    //if (txtString != null)
                    //{
                    //    // Create MleaderJig
                    //    MLeaderJig jig = new MLeaderJig(mlStyleId, mt);

                    //    // Loop to set vertices
                    //    bool bSuccess = true, bComplete = false;
                    //    while (bSuccess && !bComplete)
                    //    {
                    //        PromptResult dragres = ed.Drag(jig);
                    //        bSuccess = (dragres.Status == PromptStatus.OK);
                    //        if (bSuccess) jig.AddVertex();
                    //        bComplete = (dragres.Status == PromptStatus.None);
                    //        if (bComplete) jig.RemoveLastVertex();
                    //    }

                    //    if (bComplete)
                    //    {
                    //        // Append entity
                    //        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead, false);
                    //        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite, false);
                    //        btr.AppendEntity(jig.GetEntity());
                    //        ent.UpgradeOpen();
                    //        ent.Erase();
                    //        tr.AddNewlyCreatedDBObject(jig.GetEntity(), true);
                    //        tr.Commit();
                    //    }
                    //}

                    //else
                    //{
                    //    ed.WriteMessage("\nBad Selection. Aborting.");
                    //    tr.Commit();
                    //}
                //}
            }
        }


        public void Text2Mleader(string mlStyleName, ObjectId MLStyleID)
        {
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            PromptEntityOptions peo = new PromptEntityOptions("\nSelect Text/MText Object: ")
            {
                AllowNone = false,
                AllowObjectOnLockedLayer = true
            };
            peo.SetRejectMessage("\nBad Selection. Select Text/MText Object: ");
            peo.AddAllowedClass(typeof(MText), false);
            peo.AddAllowedClass(typeof(DBText), false);            

            PromptEntityResult per = ed.GetEntity(peo);

            if (per.Status == PromptStatus.OK)
            {
                Transaction tr = doc.TransactionManager.StartTransaction();
                using (tr)
                {
                    string txtString = null;                    
                    double txtBoxWidth = 0;
                    double txtHeight = 0;

                    DBDictionary mlStyles = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead);
                    ObjectId mlStyleId = ObjectId.Null;

                    if (!mlStyles.Contains(mlStyleName))
                    {
                        //no style in db. use standard.
                        mlStyleId = mlStyles.GetAt("Standard");
                    }
                    else
                    {
                        mlStyleId = mlStyles.GetAt(mlStyleName);
                    }
                    db.MLeaderstyle = mlStyleId;
                    MLeaderStyle thisStyle = (MLeaderStyle)tr.GetObject(mlStyleId, OpenMode.ForRead);                                          
                    MText mt = new MText();                    
                    
                    Entity ent = (Entity)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    string entType = ent.GetType().ToString().Split('.')[ent.GetType().ToString().Split('.').Length - 1];

                    switch (entType)
                    {
                        case "MText":
                            MText tempMt = (MText)ent;
                            txtString = tempMt.getMTextWithFieldCodes();
                            txtBoxWidth = tempMt.ActualWidth;
                            txtHeight = tempMt.TextHeight;
                            break;
                        case "DBText":                            
                            DBText dbt = (DBText)ent;
                            txtString = dbt.TextString;
                            txtHeight = dbt.Height;
                            break;                            
                    }
                    if (txtString != null)
                    {
                        mt.Contents = txtString;
                        mt.TextStyleId = thisStyle.TextStyleId;
                        mt.Width = txtBoxWidth;
                        mt.TextHeight = txtHeight;

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

        private static ObjectId AddMultileaderStyle(string styleName, string arrowStyle, double arrowSize, double dogLeg, bool textFrame, double landing, double landingGap, LeaderType thisLeaderType, string textStyleName, double textHeightpt, AnnotativeStates annotatitionType, TextAttachmentType textAttachmentType)
        {
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            ObjectId mlStyle = ObjectId.Null;

            MLeaderStyle newStyle = new MLeaderStyle();

             Transaction tr = doc.TransactionManager.StartTransaction();
             using (tr)
             {
                 DBDictionary mlStyles = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead);
                 BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                 
                 if(!mlStyles.Contains(styleName))
                 {

                     if (!bt.Has(arrowStyle)) acadApp.SetSystemVariable("DIMBLK", "_DotBlank");

                     newStyle.Annotative = annotatitionType;
                     newStyle.Scale = 1;


                     newStyle.ArrowSymbolId = bt[arrowStyle];
                     newStyle.ArrowSize = arrowSize * HSKDICommon.Commands.Getdimscale();
                     newStyle.ContentType = ContentType.MTextContent;
                     newStyle.EnableFrameText = textFrame;
                     newStyle.LandingGap = landing * HSKDICommon.Commands.Getdimscale();
                     newStyle.EnableLanding = newStyle.LandingGap > 0 ? true : false;
                     newStyle.DoglegLength = dogLeg * HSKDICommon.Commands.Getdimscale();
                     newStyle.EnableDogleg = newStyle.DoglegLength * HSKDICommon.Commands.Getdimscale() > 0 ? true : false;
                     newStyle.ExtendLeaderToText = true;
                     newStyle.LeaderLineType = thisLeaderType;
                     newStyle.MaxLeaderSegmentsPoints = 2;
                     newStyle.TextHeight = textHeightpt * HSKDICommon.Commands.Getdimscale();

                     newStyle.TextAttachmentDirection = TextAttachmentDirection.AttachmentHorizontal;
                     newStyle.SetTextAttachmentType(textAttachmentType, LeaderDirectionType.LeftLeader);
                     newStyle.SetTextAttachmentType(textAttachmentType, LeaderDirectionType.RightLeader);

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
                 }
                 db.MLeaderstyle = mlStyle;
                 tr.Commit();                 
             }
             return mlStyle;
        }   
    }    
}