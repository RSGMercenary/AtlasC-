﻿using Atlas.Interfaces;
using System;
using System.Collections.Generic;

namespace Atlas.Signals
{
	class SignalBase:ISignalBase, IDispose
	{
		protected List<SlotBase> slots = new List<SlotBase>();
		private Stack<SlotBase> slotsPooled = new Stack<SlotBase>();
		private Stack<SlotBase> slotsRemoved = new Stack<SlotBase>();
		private int numDispatches = 0;

		private bool isDisposed = false;

		public SignalBase()
		{

		}

		/// <summary>
		/// Cleans up the Signal by removing and disposing all listeners,
		/// and unpooling allocated Slots.
		/// </summary>
		public void Dispose()
		{
			if(!isDisposed)
			{
				IsDisposed = true;
				slotsPooled.Clear();
				RemoveAll();
			}
		}

		public bool IsDisposed
		{
			get
			{
				return isDisposed;
			}
			private set
			{
				if(isDisposed != value)
				{
					bool previous = isDisposed;
					isDisposed = value;
				}
			}
		}

		/// <summary>
		/// Calls Dispose only if there are no listeners.
		/// </summary>
		public bool HasSlots
		{
			get
			{
				return slots.Count > 0;
			}
		}

		protected bool DispatchStart()
		{
			if(slots.Count > 0)
			{
				++numDispatches;
				return true;
			}
			return false;
		}

		protected bool DispatchStop()
		{
			--numDispatches;
			if(numDispatches == 0)
			{
				while(slotsRemoved.Count > 0)
				{
					DisposeSlot(slotsRemoved.Pop());
				}
				return true;
			}
			return false;
		}

		virtual protected void DispatchesStop()
		{

		}

		/// <summary>
		/// The number of concurrent dispatches. During a dispatch, it's possible that external
		/// code could require another dispatch on the same Signal.
		/// </summary>
		public int NumDispatches
		{
			get
			{
				return numDispatches;
			}
		}

		/// <summary>
		/// The number of Slots/listeners attached to this Signal.
		/// </summary>
		public int NumSlots
		{
			get
			{
				return slots.Count;
			}
		}

		/// <summary>
		/// Returns a copy of the Slots being processed by this Signal in order of
		/// how they're prioritized.
		/// </summary>
		public List<ISlotBase> Slots
		{
			get
			{
				return new List<ISlotBase>(slots);
			}
		}

		private void DisposeSlot(SlotBase slot)
		{
			slot.Signal = null;
			slot.Dispose();
			if(!isDisposed)
				slotsPooled.Push(slot);
		}

		public ISlotBase Get(Delegate listener)
		{
			if(listener != null)
			{
				foreach(SlotBase slot in slots)
				{
					if(slot.Listener == listener)
					{
						return slot;
					}
				}
			}
			return null;
		}

		public ISlotBase Get(int index)
		{
			if(index < 0)
				return null;
			if(index > slots.Count - 1)
				return null;
			return slots[index];
		}

		public int GetIndex(Delegate listener)
		{
			if(listener != null)
			{
				for(int index = slots.Count - 1; index > -1; --index)
				{
					if(slots[index].Listener == listener)
					{
						return index;
					}
				}
			}
			return -1;
		}

		public ISlotBase Add(Delegate listener)
		{
			return Add(listener, 0);
		}

		public ISlotBase Add(Delegate listener, int priority = 0)
		{
			if(listener != null)
			{
				SlotBase slot = (SlotBase)Get(listener);
				if(slot == null)
				{
					if(slotsPooled.Count > 0)
					{
						slot = slotsPooled.Pop();
					}
					else
					{
						slot = CreateSlot();
					}

					slot.Signal = this;
					slot.Listener = listener;
					slot.Priority = priority;

					PriorityChanged(slot, 0, 0);

					IsDisposed = false;

					return slot;
				}
			}
			return null;
		}

		virtual protected SlotBase CreateSlot()
		{
			return new SlotBase();
		}

		internal void PriorityChanged(SlotBase slot, int current, int previous)
		{
			slots.Remove(slot);

			for(int index = slots.Count; index > 0; --index)
			{
				if(slots[index - 1].Priority <= slot.Priority)
				{
					slots.Insert(index, slot);
					return;
				}
			}

			slots.Insert(0, slot);
		}

		public bool Remove(Delegate listener)
		{
			if(listener != null)
			{
				for(int index = slots.Count - 1; index > -1; --index)
				{
					if(slots[index].Listener == listener)
					{
						return Remove(index);
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Removes the Slot/listener at the given index.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public bool Remove(int index)
		{
			if(index < 0)
				return false;
			if(index >= slots.Count)
				return false;
			SlotBase slot = slots[index];
			slots.RemoveAt(index);
			if(numDispatches > 0)
			{
				slotsRemoved.Push(slot);
			}
			else
			{
				DisposeSlot(slot);
			}
			return true;
		}

		/// <summary>
		/// Removes all Slots/listeners.
		/// </summary>
		public bool RemoveAll()
		{
			if(slots.Count <= 0)
				return false;
			while(slots.Count > 0)
			{
				Remove(slots.Count - 1);
			}
			return true;
		}
	}
}