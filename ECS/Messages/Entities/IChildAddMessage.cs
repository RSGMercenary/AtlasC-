﻿using Atlas.ECS.Entities;

namespace Atlas.Framework.Messages
{
	public interface IChildAddMessage : IKeyValueMessage<IEntity, int, IEntity>
	{

	}
}