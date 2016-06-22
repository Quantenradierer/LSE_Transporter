using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.ModAPI;
using VRage.ObjectBuilders;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.ModAPI;

namespace LSE.Control
{
    public class Textbox<T> : BaseControl<T>
    {
        string DefaultValue;

        public Textbox(
            IMyTerminalBlock block,
            string internalName,
            string title,
            string defaultValue)
            : base(block, internalName, title)
        {
            DefaultValue = defaultValue;
        }

        public override void OnCreateUI()
        {
            var control = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, T>(InternalName);
            control.Visible = ShowControl;
            control.Getter = Getter;
            control.Setter = Setter;
            control.Title = VRage.Utils.MyStringId.GetOrCompute(Title);
            MyAPIGateway.TerminalControls.AddControl<T>(control);
        }

        public virtual StringBuilder Getter(IMyTerminalBlock block)
        {
            string value = "";
            MyAPIGateway.Utilities.GetVariable<string>(block.EntityId.ToString() + InternalName, out value);

            return new StringBuilder(value);
        }

        public virtual void Setter(IMyTerminalBlock block, StringBuilder builder)
        {
            MyAPIGateway.Utilities.SetVariable<string>(block.EntityId.ToString() + InternalName, builder.ToString());
        }
    }
}
