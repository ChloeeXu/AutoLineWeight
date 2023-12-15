using System;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input.Custom;
using Rhino.Input;

namespace AutoLineWeight
{
    public class SimpleSelect : Command
    {
        string promptName;
        ObjRef[] simpleSelection;

        public SimpleSelect(string promptName)
        {
            this.promptName = promptName;
            Instance = this;
        }

        ///<summary>The only instance of the MyCommand command.</summary>
        public static SimpleSelect Instance { get; private set; }

        public override string EnglishName => "SimpleSelect";

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
            getObject.SetCommandPrompt("Select geometry for " + promptName);
            getObject.GroupSelect = true;
            getObject.EnableClearObjectsOnEntry(false);
            getObject.EnableUnselectObjectsOnExit(false);
            getObject.DeselectAllBeforePostSelect = false;

            bool hasPreselect = false;

            // For loop runs once when there are no preselected geometry, twice when there is
            // Ensures that both preselection and postselection
            for (; ; )
            {
                GetResult res = getObject.GetMultiple(1, 0); // This does not clear when called again?

                if (res != GetResult.Object)
                {
                    this.Deselect();
                    doc.Views.Redraw();
                    RhinoApp.WriteLine("No valid geometry was selected.");
                    return Result.Cancel;
                }

                else if (getObject.ObjectsWerePreselected)
                {
                    hasPreselect = true;
                    getObject.EnablePreSelect(false, true);
                    continue;
                }

                break;
            }

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

            simpleSelection = getObject.Objects();

            this.Deselect();
            doc.Views.Redraw();

            return Result.Success;
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

        public Rhino.DocObjects.ObjRef[] GetSimpleSelection()
        {
            this.RunCommand(RhinoDoc.ActiveDoc, RunMode.Interactive);
            return this.simpleSelection;
        }
    }
}