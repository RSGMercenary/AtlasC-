﻿namespace Atlas.Engine.Collections.LinkList
{
	interface IReadOnlyLinkList<T>
	{
		ILinkListNode<T> First { get; }
		ILinkListNode<T> Last { get; }
		int Count { get; }
		bool Contains(T data);
		int GetIndex(T data);
		bool IsEmpty { get; }

		T this[int i] { get; }
	}
}