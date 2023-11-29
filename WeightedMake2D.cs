using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;

namespace AutoLineWeight
{
    public class WeightedMake2D : Command
    {
        public WeightedMake2D()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static WeightedMake2D Instance { get; private set; }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "WeightedMake2D";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Aquires current viewport
            RhinoViewport CurrentViewport;
            CurrentViewport = RhinoDoc.ActiveDoc.Views.ActiveView.ActiveViewport;
            RhinoApp.WriteLine("Weighted Make2D running!");

            ObjRef[] objRefs = UserSelObj(doc, mode);

            if (objRefs == null)
            {
                return Result.Cancel;
            }

            RhinoApp.WriteLine("UserSelObj selected {0} objects!", objRefs.Length);



            // TODO: start here modifying the behaviour of your command.
            // ---
            /*RhinoApp.WriteLine("The {0} command will add a line right now.", EnglishName);

            Point3d pt0;
            using (GetPoint getPointAction = new GetPoint())
            {
                getPointAction.SetCommandPrompt("Please select the start point");
                if (getPointAction.Get() != GetResult.Point)
                {
                    RhinoApp.WriteLine("No start point was selected.");
                    return getPointAction.CommandResult();
                }
                pt0 = getPointAction.Point();
            }

            Point3d pt1;
            using (GetPoint getPointAction = new GetPoint())
            {
                getPointAction.SetCommandPrompt("Please select the end point");
                getPointAction.SetBasePoint(pt0, true);
                getPointAction.DynamicDraw +=
                  (sender, e) => e.Display.DrawLine(pt0, e.CurrentPoint, System.Drawing.Color.DarkRed);
                if (getPointAction.Get() != GetResult.Point)
                {
                    RhinoApp.WriteLine("No end point was selected.");
                    return getPointAction.CommandResult();
                }
                pt1 = getPointAction.Point();
            }

            doc.Objects.AddLine(pt0, pt1);
            doc.Views.Redraw();
            RhinoApp.WriteLine("The {0} command added one line to the document.", EnglishName);*/

            // ---
            return Result.Success;
        }

        /// <summary>
        /// Asks the user to select objects for the generic make2D. This method accepts both
        /// preselected and postselected geometry, deselecting both after processing. Invalid
        /// geometry remains selected.
        /// </summary>
        /// <returns> An array of user-selected objects including surfaces and polysurfaces </returns>
        private ObjRef[] UserSelObj(RhinoDoc doc, RunMode mode)
        {
            // POTENTIAL ISSUE: the result of GetObject is not returned to RunCommand.

            // Initialize getobject
            GetObject getObject = new GetObject();
            // GO settings
            getObject.GeometryFilter = ObjectType.Surface | ObjectType.PolysrfFilter;
            getObject.SubObjectSelect = true;
            getObject.SetCommandPrompt("Select geometry for the weighted make2d");
            getObject.GroupSelect = true;
            getObject.EnableClearObjectsOnEntry(false);
            getObject.EnableUnselectObjectsOnExit(true);
            getObject.DeselectAllBeforePostSelect = false;

            bool hasPreselect = false;

            // For loop runs once when there are no preselected geometry, twice when there is
            // Ensures that both preselection and postselection
            for (; ; )
            {
                GetResult res = getObject.GetMultiple(1, 0); // This does not clear when called again?

                // Case: User did not select an object
                if (res != GetResult.Object) { return null; }

                if (getObject.ObjectsWerePreselected)
                {
                    hasPreselect = true;
                    getObject.EnablePreSelect(false, true);
                    continue;
                }

                break;
            }

            // Deselects all VALID objects
            // Does not work for invalid objects, in this case, curves, for instance
            // It does not make sense that invalid objects remain selected.
            // Is this necessary? By default, preselected objects remain selected and 
            // postselected objects are unselected. Does follow default make more sense?
            if (hasPreselect)
            {
                for (int i = 0; i < getObject.ObjectCount; i++)
                {
                    RhinoObject obj = getObject.Object(i).Object();
                    if (null != obj)
                        obj.Select(false);
                }
                doc.Views.Redraw();
            }

            RhinoApp.WriteLine("A total of {0} objects were selected.", getObject.ObjectCount);
            // Turn the remain selected problem into a feature?
            RhinoApp.WriteLine("Objects that remain selected were not processed.");

            return getObject.Objects();
        }
    }
}
