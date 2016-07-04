using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage;
using VRage.ObjectBuilders;
using VRage.Game;
using VRage.ModAPI;
using VRage.Game.Components;
using VRageMath;
using Sandbox.Engine.Multiplayer;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;

namespace LSE
{

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), new string[] { "TransportJammer" })]
    class Jammer : GameLogicComponent
    {
        public static HashSet<Jammer> ScramblerList = new HashSet<Jammer>();

        static public bool IsProtected(Vector3D position, IMyCubeBlock transporterBlock)
        {
            foreach (var scrambler in ScramblerList)
            {
                var scramblerBlock = (IMyCubeBlock)scrambler.Entity;
                bool friendlyScrambler = transporterBlock.GetUserRelationToOwner(scramblerBlock.OwnerId).IsFriendly();
                    
                if (scrambler.IsProtecting(position) && !friendlyScrambler)
                {   
                    return true;
                }
            }
            return false;
        }


        public bool FirstStart = true;
        public RangeSlider<Sandbox.ModAPI.IMyOreDetector> Slider;
        public Sandbox.Game.EntityComponents.MyResourceSinkComponent Sink;
        public MyDefinitionId PowerDefinitionId = new VRage.Game.MyDefinitionId(typeof(VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GasProperties), "Electricity");



        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            
            Entity.Components.TryGet<Sandbox.Game.EntityComponents.MyResourceSinkComponent>(out Sink);
            Sink.SetRequiredInputFuncByType(PowerDefinitionId, CalcRequiredPower);

        }

        public float CalcRequiredPower()
        {
            float power = 0.0001f;
            if (((IMyCubeBlock)Entity).IsWorking)
            {
                var radius = Slider.Getter((IMyFunctionalBlock)Entity);
                power = (float)(4.0 * Math.PI * Math.Pow(radius, 3) / 3.0 / 1000.0 / 1000.0);
            }
            return power;
        }

        public override void Close()
        {
            /*
            try
            {
                Jammer.ScramblerList.Remove(this);
            }
            catch
            {
            }
            base.Close();
             * */
        }

        public override void MarkForClose()
        {
            try
            {
                Jammer.ScramblerList.Remove(this);
            }
            catch
            {
            }
            base.MarkForClose();
        }

        public override void UpdateBeforeSimulation100()
        {
            if (FirstStart)
            {
                CreateUI();
                ((IMyFunctionalBlock)Entity).AppendingCustomInfo += AppendingCustomInfo;
                FirstStart = false;
            }
            ScramblerList.Add(this);
        }

        void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            var jammer = block.GameLogic.GetAs<Jammer>();
            stringBuilder.Clear();
            stringBuilder.Append("Required Power: " + jammer.CalcRequiredPower().ToString("0.00") + "MW");
        }

        public bool IsProtecting(Vector3D postion)
        {
            if (((IMyFunctionalBlock)Entity).IsWorking && ((IMyFunctionalBlock)Entity).IsFunctional)
            {
                return Math.Pow(GetRadius(), 2) > (Entity.GetPosition() - postion).LengthSquared(); 
            }
            return false;
        }

        float GetRadius()
        {
            return Slider.Getter((IMyTerminalBlock)Entity);
        }

        void RemoveOreUI()
        {
            List<IMyTerminalAction> actions = new List<IMyTerminalAction>();
            MyAPIGateway.TerminalControls.GetActions<Sandbox.ModAPI.IMyOreDetector>(out actions);
            var actionAntenna = actions.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            actionAntenna.Enabled = ShowControlOreDetectorControls;

            List<IMyTerminalControl> controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<Sandbox.ModAPI.IMyOreDetector>(out controls);
            var antennaControl = controls.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            antennaControl.Visible = ShowControlOreDetectorControls;
            // var radiusControl = controls.First((x) => x.Id.ToString() == "Range");
            // radiusControl.Visible = ShowControlOreDetectorControls;
        }

        bool ShowControlOreDetectorControls(IMyTerminalBlock block)
        {
            return block.BlockDefinition.SubtypeName.Contains("OreDetector");
        }

        public void CreateUI()
        {
            RemoveOreUI();

            Slider = new RangeSlider<Sandbox.ModAPI.IMyOreDetector>((IMyFunctionalBlock)Entity,
                "RadiusSlider",
                "Transporter Jammer Radius",
                10,
                500,
                50);
        }
    }


    class RangeSlider<T> : Control.Slider<T>
    {

        public RangeSlider(
            IMyTerminalBlock block,
            string internalName,
            string title,
            float min = 0.0f,
            float max = 100.0f,
            float standard = 10.0f)            
            : base(block, internalName, title, min, max, standard)
        {
        }

        public override void Writer(IMyTerminalBlock block, StringBuilder builder)
        {
            try
            {
                builder.Clear();
                var distanceString = Getter(block).ToString("0") + "m";
                builder.Append(distanceString);
                block.RefreshCustomInfo();
            }
            catch
            {
            }
        }

        public void SetterOutside(IMyTerminalBlock block, float value)
        {
            base.Setter(block, value);
            block.GameLogic.GetAs<Jammer>().Sink.Update();
        }

        public override void Setter(IMyTerminalBlock block, float value)
        {
            base.Setter(block, value);
            var message = new JammerNetwork.MessageSync() { Value = value, EntityId = block.EntityId };
            JammerNetwork.MessageUtils.SendMessageToAll(message);
            var jammer = block.GameLogic.GetAs<Jammer>();
            jammer.Sink.Update();
        }
    }
}
