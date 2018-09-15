﻿using Atlas.Core.Messages;
using Atlas.ECS.Components;
using Atlas.ECS.Systems;
using System;

namespace Atlas.ECS.Messages
{
	class SystemAddMessage : KeyValueMessage<IEngine, Type, IReadOnlySystem>, ISystemAddMessage
	{
		public SystemAddMessage(IEngine messenger, Type key, IReadOnlySystem value) : base(messenger, key, value)
		{
		}
	}
}
