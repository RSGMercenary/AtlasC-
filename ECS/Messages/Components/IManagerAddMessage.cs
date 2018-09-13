﻿using Atlas.ECS.Components;
using Atlas.ECS.Entities;

namespace Atlas.Framework.Messages
{
	public interface IManagerAddMessage : IKeyValueMessage<IComponent, int, IEntity>
	{

	}
}
