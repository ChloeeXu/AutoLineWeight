/*
------------------------------

This class accesses user-selected geometry. It accepts both
preselected and postselected geometry, deselecting both after 
processing. Invalid geometry remains selected.

------------------------------
created 11/30/2023
Ennead Architects

Chloe Xu
chloe.xu@ennead.com
edited:11/30/2023

------------------------------
*/


using System;
using System.Runtime.Remoting;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input.Custom;
using Rhino.Input;
using System.Diagnostics;
using Rhino.UI;

namespace AutoLineWeight
{
    public class UserSelectGeometry : Command
    {
        // Storage location for selection.
        Rhino.DocObjects.ObjRef[] userSelection;

        /// <summary>
        /// Aqures user selection of multiple geometry. Accepts polysurfaces
        /// and curves.
        /// </summary>
        public UserSelectGeometry()
        {
            Instance = this;
        }

        ///<summary>The only instance of the MyCommand command.</summary>
        public static UserSelectGeometry Instance { get; private set; }

        public override string EnglishName => "UserSelectGeometry";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Initialize getobject
            GetObject getObject = new GetObject();
            // GO settings
            getObject.GeometryFilter =
                ObjectType.Surface |
                ObjectType.PolysrfFilter |
                ObjectType.Brep |
                ObjectType.Curve;
            getObject.SubObjectSelect = true;
            getObject.SetCommandPrompt("Select geometry for the weighted make2d");
            getObject.GroupSelect = true;
            getObject.EnableClearObjectsOnEntry(false);
            getObject.EnableUnselectObjectsOnExit(true);
            getObject.DeselectAllBeforePostSelect = false;
            getObject.EnableUnselectObjectsOnExit(true);

            bool includeClipping = true;
            bool includeHidden = false;

            bool hasPreselect = false;

            // For loop runs once when there are no preselected geometry, twice when there is
            // Ensures that both preselection and postselection
            for (; ; )
            {
                GetResult res = getObject.GetMultiple(1, 0); // This does not clear when called again?

                // Case: User did not select an object
                if (res != GetResult.Object) 
                {
                    this.Deselect();
                    doc.Views.Redraw();
                    RhinoApp.WriteLine("No valid geometry was selected.");
                    return Result.Cancel; 
                }

                if (getObject.ObjectsWerePreselected)
                {
                    hasPreselect = true;
                    getObject.EnablePreSelect(false, true);
                    continue;
                }

                break;
            }

            // Deselects all VALID objects
            // Does not work for invalid objects, in this case, meshes, for instance
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

            userSelection = getObject.Objects();

            this.Deselect();
            doc.Views.Redraw();

            return Result.Success;
        }

        /// <summary>
        /// Requests and gets the selected geometry.
        /// </summary>
        /// <returns> An array of ObjRef </returns>
        public Rhino.DocObjects.ObjRef[] GetUserSelection ()
        {
            this.RunCommand(RhinoDoc.ActiveDoc, RunMode.Interactive);
            return this.userSelection;
        }

        private void Deselect() 
        {
            GetObject getRemaining = new GetObject();
            getRemaining.EnablePreSelect(true, false);
            getRemaining.EnablePostSelect(false);

            GetResult rem = getRemaining.GetMultiple(1, 0); // This does not clear when called again?

            // Case: User did not select an object
            if (rem == GetResult.Object)
            {
                if (getRemaining.ObjectsWerePreselected)
                {
                    for (int i = 0; i < getRemaining.ObjectCount; i++)
                    {
                        RhinoObject obj = getRemaining.Object(i).Object();
                        if (null != obj)
                            obj.Select(false);
                    }
                }
            }
        }
    }
}