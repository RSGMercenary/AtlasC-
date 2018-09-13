﻿using Atlas.ECS.Entities;

namespace Atlas.Framework.Messages
{
	class ParentIndexMessage : PropertyMessage<IEntity, int>, IParentIndexMessage
	{
		public ParentIndexMessage(IEntity messenger, int current, int previous) : base(messenger, current, previous)
		{
		}
	}
}
