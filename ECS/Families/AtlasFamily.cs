﻿using Atlas.Core.Collections.Group;
using Atlas.Core.Collections.Pool;
using Atlas.Core.Messages;
using Atlas.Core.Objects;
using Atlas.ECS.Components;
using Atlas.ECS.Entities;
using Atlas.ECS.Families.Messages;
using Atlas.ECS.Objects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Atlas.ECS.Families
{
	sealed class AtlasFamily<TFamilyMember> : AtlasObject, IFamily<TFamilyMember>
		where TFamilyMember : class, IFamilyMember, new()
	{
		#region Fields

		//Reflection Fields
		private readonly Type family;
		private readonly BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
		private readonly Dictionary<Type, string> components = new Dictionary<Type, string>();

		//Family Members
		private readonly Group<TFamilyMember> members = new Group<TFamilyMember>();
		private readonly Dictionary<IEntity, TFamilyMember> entities = new Dictionary<IEntity, TFamilyMember>();

		//Pooling
		private readonly Stack<TFamilyMember> removed = new Stack<TFamilyMember>();
		private readonly Pool<TFamilyMember> pool = new Pool<TFamilyMember>();

		#endregion

		#region Compose / Dispose

		public AtlasFamily()
		{
			family = typeof(TFamilyMember);
			foreach(var field in typeof(TFamilyMember).GetFields(flags))
			{
				components.Add(field.FieldType, field.Name);
			}
		}

		public sealed override void Dispose()
		{
			//Can't destroy Family mid-update.
			if(Engine != null || removed.Count > 0)
				return;
			base.Dispose();
		}

		protected override void Disposing()
		{
			//TO-DO
			//Do some clean up maybe? Or let the GC handle it.
			base.Disposing();
		}

		protected override void RemovingEngine(IEngine engine)
		{
			base.RemovingEngine(engine);
			Dispose();
		}

		#endregion

		#region Iteration

		IReadOnlyGroup<IFamilyMember> IFamily.Members => Members;
		public IReadOnlyGroup<TFamilyMember> Members { get { return members; } }

		public IEnumerator<TFamilyMember> GetEnumerator()
		{
			return members.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion

		#region Engine

		public sealed override IEngine Engine
		{
			get { return base.Engine; }
			set
			{
				if(value != null)
				{
					if(Engine == null && value.HasFamily(this))
					{
						base.Engine = value;
					}
				}
				else
				{
					if(Engine != null && !Engine.HasFamily(this))
					{
						base.Engine = value;
					}
				}
			}
		}

		#endregion

		#region Member Add/Remove

		public void AddEntity(IEntity entity)
		{
			Add(entity);
		}

		public void RemoveEntity(IEntity entity)
		{
			Remove(entity);
		}

		public void AddEntity(IEntity entity, Type componentType)
		{
			if(components.ContainsKey(componentType))
			{
				Add(entity);
			}
		}

		public void RemoveEntity(IEntity entity, Type componentType)
		{
			if(components.ContainsKey(componentType))
			{
				Remove(entity);
			}
		}

		private void Add(IEntity entity)
		{
			if(entity.Engine != Engine)
				return;
			if(entities.ContainsKey(entity))
				return;
			foreach(var type in components.Keys)
			{
				if(!entity.HasComponent(type))
					return;
			}
			var member = SetMemberValues(pool.Remove(), entity, true);
			members.Add(member);
			entities.Add(entity, member);
			Message<IFamilyMemberAddMessage<TFamilyMember>>(new FamilyMemberAddMessage<TFamilyMember>(this, member));
		}

		private void Remove(IEntity entity)
		{
			if(entity.Engine != Engine)
				return;
			if(!entities.ContainsKey(entity))
				return;
			var member = entities[entity];
			entities.Remove(entity);
			members.Remove(member);
			Message<IFamilyMemberRemoveMessage<TFamilyMember>>(new FamilyMemberRemoveMessage<TFamilyMember>(this, member));

			if(Engine == null || Engine.UpdateState == TimeStep.None)
			{
				DisposeMember(member);
			}
			else
			{
				removed.Push(member);
				Engine.AddListener<IUpdateStateMessage>(PoolMembers);
			}
		}

		#endregion

		#region Member Pooling

		private void PoolMembers(IUpdateStateMessage message)
		{
			//Clean up update listener.
			if(message.CurrentValue != TimeStep.None)
				return;
			message.Messenger.RemoveListener<IUpdateStateMessage>(PoolMembers);
			while(removed.Count > 0)
				DisposeMember(removed.Pop());
			if(Engine == null)
				Dispose();
		}

		private void DisposeMember(TFamilyMember member)
		{
			pool.Add(SetMemberValues(member, null, false));
		}

		#endregion

		#region Utility Methods

		private TFamilyMember SetMemberValues(TFamilyMember member, IEntity entity, bool add)
		{
			family.BaseType.GetField("entity", flags).SetValue(member, entity);
			foreach(var type in components.Keys)
			{
				var component = add ? entity.GetComponent(type) : null;
				family.GetField(components[type], flags).SetValue(member, component);
			}
			return member;
		}

		public void Sort(Action<IList<TFamilyMember>, Func<TFamilyMember, TFamilyMember, int>> sort, Func<TFamilyMember, TFamilyMember, int> compare)
		{
			sort(members, compare);
		}

		#endregion
	}
}