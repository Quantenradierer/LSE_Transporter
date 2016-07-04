/*
Copyright © 2016 Leto
This work is free. You can redistribute it and/or modify it under the
terms of the Do What The Fuck You Want To Public License, Version 2,
as published by Sam Hocevar. See http://www.wtfpl.net/ for more details.
*/

using System.Collections.Generic;
using System.Text;

using Sandbox.ModAPI;
using VRage.ObjectBuilders;
using Sandbox.ModAPI.Interfaces.Terminal;

namespace LSE.Control
{
    public class Checkbox<T> : BaseControl<T>
    {
        public bool DefaultValue;

        public Checkbox(
            IMyTerminalBlock block,
            string internalName,
            string title,
            bool defaultValue = true)
            : base(block, internalName, title)
        {
            DefaultValue = defaultValue;

            bool temp;
            if (!MyAPIGateway.Utilities.GetVariable<bool>(block.EntityId.ToString() + InternalName, out temp))
            {
                MyAPIGateway.Utilities.SetVariable<bool>(block.EntityId.ToString() + InternalName, defaultValue);
            }
        }

        public override void OnCreateUI()
        {
            var checkbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, T>(InternalName);
            checkbox.Visible = ShowControl;
            checkbox.Getter = Getter;
            checkbox.Setter = Setter;
            checkbox.Title = VRage.Utils.MyStringId.GetOrCompute(Title);
            MyAPIGateway.TerminalControls.AddControl<T>(checkbox);
        }

        public virtual bool Getter(IMyTerminalBlock block)
        {
            bool value = DefaultValue;
            MyAPIGateway.Utilities.GetVariable<bool>(block.EntityId.ToString() + InternalName, out value);
            return value;
        }

        public virtual void Setter(IMyTerminalBlock block, bool newState)
        {
            MyAPIGateway.Utilities.SetVariable<bool>(block.EntityId.ToString() + InternalName, newState);
        }

    }
}