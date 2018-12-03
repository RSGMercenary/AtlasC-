﻿using Atlas.Core.Messages;
using Atlas.ECS.Components;
using System;

namespace Atlas.ECS.Entities.Messages
{
	class ComponentAddMessage : KeyValueMessage<IEntity, Type, IComponent>, IComponentAddMessage
	{
		public ComponentAddMessage(IEntity messenger, Type key, IComponent value) : base(messenger, key, value)
		{
		}
	}
}