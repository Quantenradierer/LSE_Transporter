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
    class Seperator<T> : BaseControl<T>
    {
        public Seperator(
            IMyTerminalBlock block,
            string internalName)
            : base(block, internalName, "")
        {
            CreateUI();
        }

        public override void OnCreateUI()
        {
            var seperator = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>(InternalName);
            seperator.Visible = ShowControl;
//            MyAPIGateway.TerminalControls.AddControl<T>(seperator);
        }

    }
}
