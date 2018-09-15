﻿using Atlas.Core.Messages;
using System;

namespace Atlas.Core.Objects
{
	public abstract class AtlasObject : MessageDispatcher, IObject
	{
		public static implicit operator bool(AtlasObject atlasObject)
		{
			return atlasObject != null;
		}

		private ObjectState state = ObjectState.Disposed;

		public AtlasObject()
		{
			Compose(true);
		}

		~AtlasObject()
		{
			Dispose(true);
		}

		public ObjectState State
		{
			get { return state; }
			private set
			{
				if(state == value)
					return;
				var previous = state;
				state = value;
				Message<IObjectStateMessage>(new ObjectStateMessage(this, value, previous));
			}
		}

		internal void Compose()
		{
			Compose(false);
		}

		private void Compose(bool constructor)
		{
			if(state != ObjectState.Disposed)
				return;
			State = ObjectState.Composing;
			Composing(constructor);
			State = ObjectState.Composed;
			GC.ReRegisterForFinalize(this);
		}

		public virtual void Dispose()
		{
			Dispose(false);
		}

		private void Dispose(bool finalizer)
		{
			if(state != ObjectState.Composed)
				return;
			State = ObjectState.Disposing;
			Disposing(finalizer);
			State = ObjectState.Disposed;
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Called when this instance is being composed. Should not be called manually.
		/// </summary>
		protected virtual void Composing(bool constructor)
		{

		}

		/// <summary>
		/// Called when this instance is being disposed. Should not be called manually.
		/// </summary>
		protected virtual void Disposing(bool finalizer)
		{

		}
	}
}