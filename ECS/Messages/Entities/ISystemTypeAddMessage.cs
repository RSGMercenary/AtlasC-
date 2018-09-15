﻿using Atlas.Core.Messages;
using Atlas.ECS.Entities;
using System;

namespace Atlas.ECS.Messages
{
	public interface ISystemTypeAddMessage : IValueMessage<IEntity, Type>
	{

	}
}
