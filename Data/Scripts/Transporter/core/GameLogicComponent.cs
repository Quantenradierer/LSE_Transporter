/*
Copyright © 2016 Leto
This work is free. You can redistribute it and/or modify it under the
terms of the Do What The Fuck You Want To Public License, Version 2,
as published by Sam Hocevar. See http://www.wtfpl.net/ for more details.
*/

using VRage.ObjectBuilders;
using VRage.Game.Components;

namespace LSE
{
    public class GameLogicComponent : MyGameLogicComponent
    {
        private MyObjectBuilder_EntityBase m_objectBuilder;

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return copy ? (MyObjectBuilder_EntityBase)m_objectBuilder.Clone() : m_objectBuilder;
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            m_objectBuilder = objectBuilder;
        }
    }

}
