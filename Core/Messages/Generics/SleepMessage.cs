﻿using Atlas.Core.Objects;

namespace Atlas.Core.Messages
{
	class SleepMessage<TMessenger> : PropertyMessage<TMessenger, int>, ISleepMessage<TMessenger>
		where TMessenger : ISleep
	{
		public SleepMessage(TMessenger messenger, int current, int previous) : base(messenger, current, previous)
		{
		}
	}
}
