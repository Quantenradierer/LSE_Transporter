/*
Copyright © 2016 Leto
This work is free. You can redistribute it and/or modify it under the
terms of the Do What The Fuck You Want To Public License, Version 2,
as published by Sam Hocevar. See http://www.wtfpl.net/ for more details.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.ModAPI;
using VRage.ObjectBuilders;
using Sandbox.ModAPI.Interfaces.Terminal;


namespace LSE.Control
{
    public class ControlAction<T>
    {
        public static Dictionary<string, Dictionary<IMyTerminalBlock, string>> Texts = new Dictionary<string, Dictionary<IMyTerminalBlock, string>>();
        public static Dictionary<string, Dictionary<IMyTerminalBlock, Action>> Functions = new Dictionary<string, Dictionary<IMyTerminalBlock, Action>>();


        public SerializableDefinitionId Definition;
        public string InternalName;
        public string Name;
        public string HotbarText;

        public ControlAction(
            IMyTerminalBlock block,
            string internalName,
            string name,
            string hotbarText,
            Action function)
        {
            Name = name;
            Definition = block.BlockDefinition;
            InternalName = internalName + Definition.SubtypeId;

            if (!Texts.ContainsKey(InternalName))
            {
                Texts[InternalName] = new Dictionary<IMyTerminalBlock, string>();
            }
            Texts[InternalName][block] = hotbarText;

            if (!Functions.ContainsKey(InternalName))
            {
                Functions[InternalName] = new Dictionary<IMyTerminalBlock, Action>();
            }
            Functions[InternalName][block] = function;

            var controls = new List<IMyTerminalAction>();
            MyAPIGateway.TerminalControls.GetActions<T>(out controls);
            var control = controls.Find((x) => x.Id.ToString() == InternalName);
            if (control == null)
            {
                var action = MyAPIGateway.TerminalControls.CreateAction<T>(InternalName);
                action.Action = OnAction;
                action.Name = new StringBuilder(Name);
                action.Enabled = Visible;
                MyAPIGateway.TerminalControls.AddAction<T>(action);
            }
        }

        public void OnAction(IMyTerminalBlock block)
        {
            Functions[InternalName][block]();
        }

        public virtual bool Visible(IMyTerminalBlock block)
        {
            return block.BlockDefinition.TypeId == Definition.TypeId &&
                    block.BlockDefinition.SubtypeId == Definition.SubtypeId;
        }

        

    }

}
