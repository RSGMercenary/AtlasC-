﻿using Atlas.Engine.Collections.Fixed;
using Atlas.Engine.Collections.LinkList;
using Atlas.Engine.Components;
using Atlas.Engine.Entities;
using Atlas.Engine.Families;
using Atlas.Engine.Signals;
using Atlas.Engine.Systems;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Atlas.Engine.Engine
{
	sealed class AtlasEngine:AtlasComponent<IEngine>, IEngine
	{
		private static AtlasEngine instance;

		private Type defaultEntity = AtlasEngineDefaults.DefaultEntity;
		private Type defaultFamily = AtlasEngineDefaults.DefaultFamily;

		private LinkList<IEntity> entities = new LinkList<IEntity>();
		private LinkList<IFamily> families = new LinkList<IFamily>();
		private LinkList<ISystem> systems = new LinkList<ISystem>();

		private Dictionary<string, IEntity> entitiesGlobalName = new Dictionary<string, IEntity>();
		private Dictionary<Type, IFamily> familiesType = new Dictionary<Type, IFamily>();
		private Dictionary<Type, ISystem> systemsType = new Dictionary<Type, ISystem>();

		private FixedStack<IEntity> entityPool = new FixedStack<IEntity>(AtlasEngineDefaults.DefaultEntityPoolCapacity);
		private FixedStack<IFamily> familyPool = new FixedStack<IFamily>(AtlasEngineDefaults.DefaultFamilyPoolCapacity);

		private Dictionary<Type, int> familiesCount = new Dictionary<Type, int>();
		private Dictionary<Type, int> systemsCount = new Dictionary<Type, int>();

		private List<IFamily> familiesRemoved = new List<IFamily>();
		private List<ISystem> systemsRemoved = new List<ISystem>();


		private Stopwatch timer = new Stopwatch();
		private int sleeping = 1;
		private bool isUpdating = false;

		private double deltaUpdateTime = 0;
		private double totalUpdateTime = 0;
		private ISystem currentUpdateSystem;

		private double deltaFixedUpdateTime = 1 / 60;
		private double totalFixedUpdateTime = 0;
		private ISystem currentFixedUpdateSystem;

		private double deltaEngineTime = 0;
		private double totalEngineTime = 0;

		private Signal<IEngine, IEntity> entityAdded = new Signal<IEngine, IEntity>();
		private Signal<IEngine, IEntity> entityRemoved = new Signal<IEngine, IEntity>();
		private Signal<IEngine, Type> familyAdded = new Signal<IEngine, Type>();
		private Signal<IEngine, Type> familyRemoved = new Signal<IEngine, Type>();
		private Signal<IEngine, Type> systemAdded = new Signal<IEngine, Type>();
		private Signal<IEngine, Type> systemRemoved = new Signal<IEngine, Type>();
		private Signal<IEngine, int, int> sleepingChanged = new Signal<IEngine, int, int>();
		private Signal<IEngine, bool> isUpdatingChanged = new Signal<IEngine, bool>();

		private AtlasEngine()
		{

		}

		public static AtlasEngine Instance
		{
			get
			{
				if(instance == null)
					instance = new AtlasEngine();
				return instance;
			}
		}

		public FixedStack<IEntity> EntityPool { get { return entityPool; } }
		public FixedStack<IFamily> FamilyPool { get { return familyPool; } }

		public IReadOnlyLinkList<IEntity> Entities { get { return entities; } }
		public IReadOnlyLinkList<IFamily> Families { get { return families; } }
		public IReadOnlyLinkList<ISystem> Systems { get { return systems; } }

		public ISignal<IEngine, IEntity> EntityAdded { get { return entityAdded; } }
		public ISignal<IEngine, IEntity> EntityRemoved { get { return entityRemoved; } }
		public ISignal<IEngine, Type> FamilyAdded { get { return familyAdded; } }
		public ISignal<IEngine, Type> FamilyRemoved { get { return familyRemoved; } }
		public ISignal<IEngine, Type> SystemAdded { get { return systemAdded; } }
		public ISignal<IEngine, Type> SystemRemoved { get { return systemRemoved; } }
		public ISignal<IEngine, int, int> SleepingChanged { get { return sleepingChanged; } }
		public ISignal<IEngine, bool> IsUpdatingChanged { get { return isUpdatingChanged; } }

		override protected void AddingManager(IEntity entity, int index)
		{
			base.AddingManager(entity, index);
			AddEntity(entity);
			entity.ParentChanged.Remove(EntityParentChanged);
			entity.Parent = null;
			--Sleeping;
		}

		override protected void RemovingManager(IEntity entity, int index)
		{
			RemoveEntity(entity);
			base.RemovingManager(entity, index);
			++Sleeping;
		}

		#region Entities

		public Type DefaultEntity
		{
			get
			{
				return defaultEntity;
			}
			set
			{
				if(defaultEntity == value)
					return;
				defaultEntity = value;
				entityPool.Clear();
			}
		}

		public IEntity GetEntity()
		{
			IEntity entity;
			if(entityPool.Count > 0)
			{
				entity = entityPool.Pop();
			}
			else
			{
				try
				{
					entity = Activator.CreateInstance(defaultEntity) as IEntity;
				}
				catch(Exception e)
				{
					Debug.WriteLine(e);
					return null;
				}
			}
			entity.Disposed.Add(EntityDisposed, int.MinValue);
			return entity;
		}

		public bool HasEntity(string globalName)
		{
			return !string.IsNullOrWhiteSpace(globalName) && entitiesGlobalName.ContainsKey(globalName);
		}

		public bool HasEntity(IEntity entity)
		{
			return entity != null && entitiesGlobalName.ContainsKey(entity.GlobalName) && entitiesGlobalName[entity.GlobalName] == entity;
		}

		public IEntity GetEntity(string globalName)
		{
			return entitiesGlobalName.ContainsKey(globalName) ? entitiesGlobalName[globalName] : null;
		}

		private void AddEntity(IEntity entity)
		{
			if(entitiesGlobalName.ContainsKey(entity.GlobalName) && entitiesGlobalName[entity.GlobalName] != entity)
			{
				entity.GlobalName = Guid.NewGuid().ToString("N");
			}
			if(!entitiesGlobalName.ContainsKey(entity.GlobalName))
			{
				entitiesGlobalName.Add(entity.GlobalName, entity);
				entities.Add(entity);

				entity.ChildAdded.Add(EntityChildAdded, int.MinValue);
				entity.ParentChanged.Add(EntityParentChanged, int.MinValue);
				entity.GlobalNameChanged.Add(EntityGlobalNameChanged, int.MinValue);
				entity.ComponentAdded.Add(EntityComponentAdded, int.MinValue);
				entity.ComponentRemoved.Add(EntityComponentRemoved, int.MinValue);
				entity.SystemAdded.Add(EntitySystemAdded, int.MinValue);
				entity.SystemRemoved.Add(EntitySystemRemoved, int.MinValue);

				entity.Engine = this;

				foreach(Type type in entity.Systems)
				{
					EntitySystemAdded(entity, type);
				}

				for(var current = families.First; current != null; current = current.Next)
				{
					current.Value.AddEntity(entity);
				}

				entityAdded.Dispatch(this, entity);

				for(var current = entity.Children.First; current != null; current = current.Next)
				{
					AddEntity(current.Value);
				}
			}
		}

		private void RemoveEntity(IEntity entity)
		{
			for(var current = entity.Children.First; current != null; current = current.Next)
			{
				RemoveEntity(current.Value);
			}

			entitiesGlobalName.Remove(entity.GlobalName);
			entities.Remove(entity);

			entity.ChildAdded.Remove(EntityChildAdded);
			entity.ParentChanged.Remove(EntityParentChanged);
			entity.GlobalNameChanged.Remove(EntityGlobalNameChanged);
			entity.ComponentAdded.Remove(EntityComponentAdded);
			entity.ComponentRemoved.Remove(EntityComponentRemoved);
			entity.SystemAdded.Remove(EntitySystemAdded);
			entity.SystemRemoved.Remove(EntitySystemRemoved);

			foreach(Type type in entity.Systems)
			{
				EntitySystemRemoved(entity, type);
			}

			for(var current = families.First; current != null; current = current.Next)
			{
				current.Value.RemoveEntity(entity);
			}

			entity.Engine = null;

			entityRemoved.Dispatch(this, entity);
		}

		private void EntityChildAdded(IEntity parent, IEntity child, int index)
		{
			AddEntity(child);
		}

		private void EntityParentChanged(IEntity child, IEntity next, IEntity previous)
		{
			if(next != null)
				return;
			RemoveEntity(child);
		}

		private void EntityGlobalNameChanged(IEntity entity, string next, string previous)
		{
			entitiesGlobalName.Remove(previous);
			entitiesGlobalName.Add(next, entity);
		}

		private void EntityDisposed(IEntity entity)
		{
			entity.Disposed.Remove(EntityDisposed);
			if(defaultEntity.IsInstanceOfType(entity))
				entityPool.Push(entity);
		}

		#endregion

		#region Systems

		public double DeltaUpdateTime
		{
			get
			{
				return deltaUpdateTime;
			}
			private set
			{
				if(deltaUpdateTime == value)
					return;
				deltaUpdateTime = value;
			}
		}

		public double TotalUpdateTime
		{
			get
			{
				return totalUpdateTime;
			}
			private set
			{
				if(totalUpdateTime == value)
					return;
				totalUpdateTime = value;
			}
		}

		public double DeltaFixedUpdateTime
		{
			get
			{
				return deltaFixedUpdateTime;
			}
			set
			{
				deltaFixedUpdateTime = value;
			}
		}

		public double TotalFixedUpdateTime
		{
			get
			{
				return totalFixedUpdateTime;
			}
			private set
			{
				if(totalFixedUpdateTime == value)
					return;
				totalFixedUpdateTime = value;
			}
		}
		public double DeltaEngineTime
		{
			get
			{
				return deltaEngineTime;
			}
			private set
			{
				if(deltaEngineTime == value)
					return;
				deltaEngineTime = value;
			}
		}
		public double TotalEngineTime
		{
			get
			{
				return totalEngineTime;
			}
			private set
			{
				if(totalEngineTime == value)
					return;
				totalEngineTime = value;
			}
		}


		public bool IsUpdating
		{
			get
			{
				return isUpdating;
			}
			private set
			{
				if(isUpdating == value)
					return;
				isUpdating = value;
				isUpdatingChanged.Dispatch(this, value);
			}
		}

		public ISystem CurrentFixedUpdateSystem
		{
			get
			{
				return currentFixedUpdateSystem;
			}
			private set
			{
				if(currentFixedUpdateSystem == value)
					return;
				currentFixedUpdateSystem = value;
			}
		}

		public ISystem CurrentUpdateSystem
		{
			get
			{
				return currentUpdateSystem;
			}
			private set
			{
				if(currentUpdateSystem == value)
					return;
				currentUpdateSystem = value;
			}
		}

		private void EntitySystemAdded(IEntity entity, Type type)
		{
			if(!systemsType.ContainsKey(type))
			{
				ISystem system;
				try
				{
					system = Activator.CreateInstance(type) as ISystem;
				}
				catch(Exception e)
				{
					Debug.WriteLine(e);
					return;
				}

				systemsType.Add(type, system);
				systemsCount.Add(type, 1);
				system.PriorityChanged.Add(SystemPriorityChanged);
				SystemPriorityChanged(system, system.Priority, 0);

				system.Engine = this;

				systemAdded.Dispatch(this, type);
			}
			else
			{
				//System was marked for removal, but was requested again during update.
				if(systemsCount[type] == 0)
					systemsRemoved.Remove(systemsType[type]);
				++systemsCount[type];
			}
		}

		private void EntitySystemRemoved(IEntity entity, Type type)
		{
			if(!systemsType.ContainsKey(type))
				return;

			if(systemsCount[type] > 0)
			{
				if(--systemsCount[type] == 0)
				{
					ISystem system = systemsType[type];

					if(IsUpdating)
					{
						systemsRemoved.Add(system);
					}
					else
					{
						DisposeSystem(system);
					}
				}
			}
		}

		private void SystemPriorityChanged(ISystem system, int next, int previous)
		{
			systems.Remove(system);

			int index = systems.Count;
			for(var current = systems.Last; current != null; current = current.Previous)
			{
				if(current.Value.Priority <= next)
				{
					systems.Add(system, index);
					return;
				}
				--index;
			}

			systems.Add(system, 0);
		}

		public bool HasSystem(ISystem system)
		{
			return systemsType.ContainsKey(system.GetType()) && systemsType[system.GetType()] == system;
		}

		public bool HasSystem<TSystem>() where TSystem : ISystem
		{
			return HasSystem(typeof(TSystem));
		}

		public bool HasSystem(Type systemType)
		{
			return systemsType.ContainsKey(systemType);
		}

		public TSystem GetSystem<TSystem>() where TSystem : ISystem
		{
			return (TSystem)GetSystem(typeof(TSystem));
		}

		public ISystem GetSystem(Type type)
		{
			return systemsType.ContainsKey(type) ? systemsType[type] : null;
		}

		public ISystem GetSystem(int index)
		{
			return systems[index];
		}

		public void Update()
		{
			if(IsSleeping)
				return;

			IsUpdating = true;

			DeltaEngineTime = timer.Elapsed.TotalSeconds - totalEngineTime;

			if(totalFixedUpdateTime < timer.Elapsed.TotalSeconds)
			{
				while(totalFixedUpdateTime < timer.Elapsed.TotalSeconds)
				{
					for(var current = systems.First; current != null; current = current.Next)
					{
						CurrentFixedUpdateSystem = current.Value;
						current.Value.FixedUpdate(deltaFixedUpdateTime);
						CurrentFixedUpdateSystem = null;
					}
					TotalFixedUpdateTime += deltaFixedUpdateTime;
				}
			}

			DeltaUpdateTime = timer.Elapsed.TotalSeconds - totalUpdateTime;
			for(var current = systems.First; current != null; current = current.Next)
			{
				CurrentUpdateSystem = current.Value;
				current.Value.Update(deltaUpdateTime);
				CurrentUpdateSystem = null;
			}
			TotalUpdateTime = timer.Elapsed.TotalSeconds;

			DisposeSystems();
			DisposeFamilies();

			TotalEngineTime = timer.Elapsed.TotalSeconds;

			IsUpdating = false;
		}

		private void DisposeSystems()
		{
			while(systemsRemoved.Count > 0)
			{
				ISystem system = systemsRemoved[0];
				systemsRemoved.RemoveAt(0);
				DisposeSystem(system);
			}
		}

		private void DisposeSystem(ISystem system)
		{
			Type type = system.GetType();
			system.PriorityChanged.Remove(SystemPriorityChanged);
			systems.Remove(system);
			systemsType.Remove(type);
			systemsCount.Remove(type);
			systemRemoved.Dispatch(this, type);
			system.Dispose();
		}

		public bool IsSleeping
		{
			get
			{
				return sleeping > 0;
			}
		}

		public int Sleeping
		{
			get
			{
				return sleeping;
			}
			set
			{
				if(sleeping == value)
					return;
				int previous = sleeping;
				sleeping = value;
				sleepingChanged.Dispatch(this, value, previous);
			}
		}

		#endregion

		#region Families

		public Type DefaultFamily
		{
			get
			{
				return defaultFamily;
			}
			set
			{
				if(defaultFamily == value)
					return;
				defaultFamily = value;
				familyPool.Clear();
			}
		}

		public bool HasFamily(IFamily family)
		{
			return familiesType.ContainsKey(family.FamilyType.GetType()) && familiesType[family.FamilyType.GetType()] == family;
		}

		public bool HasFamily<TFamilyType>()
		{
			return HasFamily(typeof(TFamilyType));
		}

		public bool HasFamily(Type type)
		{
			return familiesType.ContainsKey(type);
		}

		public IFamily AddFamily<TFamilyType>()
		{
			return AddFamily(typeof(TFamilyType));
		}

		public IFamily AddFamily(Type type)
		{
			IFamily family;

			if(!familiesType.ContainsKey(type))
			{
				if(familyPool.Count > 0)
				{
					family = familyPool.Pop();
				}
				else
				{
					try
					{
						family = Activator.CreateInstance(defaultFamily) as IFamily;
					}
					catch(Exception e)
					{
						Debug.WriteLine(e);
						return null;
					}
				}

				families.Add(family);
				familiesType.Add(type, family);
				familiesCount.Add(type, 1);
				family.FamilyType = type;
				family.Engine = this;

				for(var current = entities.First; current != null; current = current.Next)
				{
					family.AddEntity(current.Value);
				}

				familyAdded.Dispatch(this, type);
			}
			else
			{
				family = familiesType[type];
				//Family was marked for removal, but was requested again during update.
				if(familiesCount[type] == 0)
					familiesRemoved.Remove(family);
				++familiesCount[type];
			}
			return family;
		}

		public IFamily RemoveFamily<TFamilyType>()
		{
			return RemoveFamily(typeof(TFamilyType));
		}

		public IFamily RemoveFamily(Type type)
		{
			if(!familiesType.ContainsKey(type))
				return null;

			IFamily family = familiesType[type];

			if(familiesCount[type] > 0)
			{
				if(--familiesCount[type] == 0)
				{
					if(IsUpdating)
					{
						familiesRemoved.Add(family);
					}
					else
					{
						DisposeFamily(family);
					}
				}
			}

			return family;
		}

		private void DisposeFamilies()
		{
			while(familiesRemoved.Count > 0)
			{
				IFamily family = familiesRemoved[0];
				familiesRemoved.RemoveAt(0);
				DisposeFamily(family);
			}
		}

		private void DisposeFamily(IFamily family)
		{
			Type type = family.FamilyType;
			families.Remove(family);
			familiesType.Remove(type);
			familiesCount.Remove(type);
			familyRemoved.Dispatch(this, type);
			family.Dispose();
			if(defaultFamily.IsInstanceOfType(family))
				familyPool.Push(family);
		}

		public IFamily GetFamily<TFamilyType>()
		{
			return GetFamily(typeof(TFamilyType));
		}

		public IFamily GetFamily(Type type)
		{
			return familiesType.ContainsKey(type) ? familiesType[type] : null;
		}

		private void EntityComponentAdded(IEntity entity, IComponent component, Type componentType)
		{
			for(var current = families.First; current != null; current = current.Next)
			{
				current.Value.AddEntity(entity, component, componentType);
			}
		}

		private void EntityComponentRemoved(IEntity entity, IComponent component, Type componentType)
		{
			for(var current = families.First; current != null; current = current.Next)
			{
				current.Value.RemoveEntity(entity, component, componentType);
			}
		}

		#endregion
	}
}