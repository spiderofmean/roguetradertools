using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ViewerMod.State
{
    /// <summary>
    /// Global registry mapping GUIDs to live object references.
    /// Single global registry with strong references (objects stay alive until cleared).
    /// </summary>
    public class HandleRegistry
    {
        private static HandleRegistry _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets the singleton instance of the HandleRegistry.
        /// </summary>
        public static HandleRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new HandleRegistry();
                        }
                    }
                }
                return _instance;
            }
        }

        // GUID -> object mapping
        private readonly ConcurrentDictionary<Guid, object> _handles = new ConcurrentDictionary<Guid, object>();
        
        // Reverse lookup: object -> GUID (to avoid duplicating handles for same object)
        private readonly ConcurrentDictionary<object, Guid> _reverseHandles = new ConcurrentDictionary<object, Guid>(new ReferenceEqualityComparer());

        private HandleRegistry() { }

        /// <summary>
        /// Registers an object and returns its handle ID.
        /// If the object is already registered, returns the existing handle.
        /// </summary>
        public Guid Register(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            // Check if already registered
            if (_reverseHandles.TryGetValue(obj, out var existingId))
            {
                return existingId;
            }

            // Create new handle
            var handleId = Guid.NewGuid();
            
            // Try to add both mappings atomically
            if (_reverseHandles.TryAdd(obj, handleId))
            {
                _handles[handleId] = obj;
                return handleId;
            }
            
            // Race condition: another thread registered it first
            return _reverseHandles[obj];
        }

        /// <summary>
        /// Tries to get an object by its handle ID.
        /// </summary>
        public bool TryGet(Guid handleId, out object obj)
        {
            return _handles.TryGetValue(handleId, out obj);
        }

        /// <summary>
        /// Gets an object by its handle ID.
        /// </summary>
        public object Get(Guid handleId)
        {
            if (_handles.TryGetValue(handleId, out var obj))
            {
                return obj;
            }
            return null;
        }

        /// <summary>
        /// Checks if an object is already registered and returns its handle if so.
        /// </summary>
        public bool TryGetHandle(object obj, out Guid handleId)
        {
            if (obj == null)
            {
                handleId = Guid.Empty;
                return false;
            }
            return _reverseHandles.TryGetValue(obj, out handleId);
        }

        /// <summary>
        /// Gets the handle for an object, registering it if necessary.
        /// Returns null if the object is null.
        /// </summary>
        public Guid? GetOrRegister(object obj)
        {
            if (obj == null)
            {
                return null;
            }
            return Register(obj);
        }

        /// <summary>
        /// Clears all handles from the registry.
        /// </summary>
        public void Clear()
        {
            _handles.Clear();
            _reverseHandles.Clear();
            Entry.Log("Handle registry cleared");
        }

        /// <summary>
        /// Gets the count of registered handles.
        /// </summary>
        public int Count => _handles.Count;

        /// <summary>
        /// Reference equality comparer for the reverse lookup dictionary.
        /// </summary>
        private class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
