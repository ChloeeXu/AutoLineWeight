﻿/*
-----------------------------------------------------------------------------------------

This class accesses user-selected geometry. It accepts both preselected and postselected 
geometry, deselecting both after processing. This class is customized for use in 
WeightedMake2D and implements options specific to WeightedMake2D.

-----------------------------------------------------------------------------------------
created 11/30/2023
Ennead Architects

Chloe Xu
chloe.xu@ennead.com
Last edited:01/03/2024

-----------------------------------------------------------------------------------------
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
    public class WMSelector: SimpleSelector
    {
        // initialize user options
        bool includeClipping = false;
        bool includeHidden = false;
        bool includeSceneSilhouette = true;

        // initialize option toggles
        // these are set as global variables to facilitate access
        private OptionToggle optIncludeClipping;
        private OptionToggle optIncludeHidden;
        private OptionToggle optSceneSilhouette;

        /// <summary>
        /// Custom setup method for WMSelector.
        /// </summary>
        protected override void SetupGetObject(GetObject getObject)
        {
            // getobject settings
            getObject.GeometryFilter =
                ObjectType.Surface |
                ObjectType.PolysrfFilter |
                ObjectType.Brep |
                ObjectType.Curve;
            getObject.SubObjectSelect = true;
            getObject.GroupSelect = true;
            getObject.DeselectAllBeforePostSelect = false;
            getObject.EnableClearObjectsOnEntry(false);
            getObject.EnableUnselectObjectsOnExit(false);
            getObject.SetCommandPrompt("Select geometry for the weighted make2d");

            // create option toggles
            optIncludeClipping = new OptionToggle(includeClipping, "Disable", "Enable");
            optIncludeHidden = new OptionToggle(includeHidden, "Disable", "Enable");
            optSceneSilhouette = new OptionToggle(includeSceneSilhouette, "Disable", "Enable");

            // add option toggles to getObject
            getObject.AddOptionToggle("Include_Clipping_Planes", ref optIncludeClipping);
            getObject.AddOptionToggle("Include_Hidden_Lines", ref optIncludeHidden);
            getObject.AddOptionToggle("Include_Scene_Silhouette", ref optSceneSilhouette);

            // set warning
            RhinoApp.WriteLine("Warning: for the current build, including clipping planes " +
                "and generating silhouettes are mutually exclusive. If both are enabled, " +
                "scene silhouettes will be prioritized. Please process these separately.");
        }

        /// <summary>
        /// Custom property updator for WMSelector. Updates properties based on user 
        /// option input.
        /// </summary>
        protected override void ModifyOptions(GetObject getObject)
        {
            this.includeClipping = optIncludeClipping.CurrentValue;
            this.includeHidden = optIncludeHidden.CurrentValue;
            this.includeSceneSilhouette = optSceneSilhouette.CurrentValue;
        }

        public bool GetIncludeClipping ()
        {
            return this.includeClipping;
        }

        public bool GetIncludeHidden ()
        {
            return this.includeHidden;
        }

        public bool GetIndluceSceneSilhouette()
        {
            return this.includeSceneSilhouette;
        }
    }
}