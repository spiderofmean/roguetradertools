using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ViewerMod.Models;
using ReflectionMemberInfo = System.Reflection.MemberInfo;

namespace ViewerMod.State
{
    /// <summary>
    /// Reflects on objects to produce wire format responses.
    /// Returns public + internal/protected members (excludes private).
    /// </summary>
    public class ObjectInspector
    {
        private readonly HandleRegistry _registry;

        // Types considered primitive (inline values, not handles)
        private static readonly HashSet<Type> PrimitiveTypes = new HashSet<Type>
        {
            typeof(bool),
            typeof(byte), typeof(sbyte),
            typeof(short), typeof(ushort),
            typeof(int), typeof(uint),
            typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal),
            typeof(char), typeof(string),
            typeof(DateTime), typeof(TimeSpan),
            typeof(Guid)
        };

        public ObjectInspector(HandleRegistry registry)
        {
            _registry = registry;
        }

        /// <summary>
        /// Inspects an object by its handle ID and returns the wire format response.
        /// </summary>
        public InspectResponse Inspect(Guid handleId)
        {
            if (!_registry.TryGet(handleId, out var obj))
            {
                return null;
            }

            return InspectObject(handleId, obj);
        }

        private InspectResponse InspectObject(Guid handleId, object obj)
        {
            if (obj == null)
            {
                return new InspectResponse
                {
                    HandleId = handleId.ToString(),
                    Type = "null",
                    AssemblyName = null,
                    Value = null,
                    Members = new List<MemberData>(),
                    CollectionInfo = null
                };
            }

            var type = obj.GetType();
            var response = new InspectResponse
            {
                HandleId = handleId.ToString(),
                Type = GetTypeName(type),
                AssemblyName = type.Assembly.GetName().Name,
                Value = GetSafeToString(obj),
                Members = new List<MemberData>(),
                CollectionInfo = null
            };

            // Check if it's a collection
            if (IsCollection(obj, out var collectionInfo))
            {
                response.CollectionInfo = collectionInfo;
            }

            // Get members (fields and properties)
            response.Members = GetMembers(obj, type);

            return response;
        }

        private List<MemberData> GetMembers(object obj, Type type)
        {
            var members = new List<MemberData>();

            // Binding flags for public + internal/protected (non-private)
            var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Get fields
            foreach (var field in type.GetFields(bindingFlags))
            {
                // Skip private fields (only include public, internal, protected)
                if (field.IsPrivate) continue;
                
                // Skip compiler-generated backing fields
                if (field.Name.StartsWith("<")) continue;

                try
                {
                    var value = field.GetValue(obj);
                    members.Add(CreateMemberInfo(field.Name, field.FieldType, value));
                }
                catch (Exception ex)
                {
                    members.Add(new MemberData
                    {
                        Name = field.Name,
                        Type = GetTypeName(field.FieldType),
                        AssemblyName = field.FieldType.Assembly.GetName().Name,
                        IsPrimitive = false,
                        HandleId = null,
                        Value = $"<error: {ex.Message}>"
                    });
                }
            }

            // Get properties
            foreach (var prop in type.GetProperties(bindingFlags))
            {
                // Skip indexers
                if (prop.GetIndexParameters().Length > 0) continue;

                // Skip properties without a getter
                var getter = prop.GetGetMethod(true);
                if (getter == null) continue;

                // Skip private getters
                if (getter.IsPrivate) continue;

                try
                {
                    var value = prop.GetValue(obj, null);
                    members.Add(CreateMemberInfo(prop.Name, prop.PropertyType, value));
                }
                catch (Exception ex)
                {
                    members.Add(new MemberData
                    {
                        Name = prop.Name,
                        Type = GetTypeName(prop.PropertyType),
                        AssemblyName = prop.PropertyType.Assembly.GetName().Name,
                        IsPrimitive = false,
                        HandleId = null,
                        Value = $"<error: {ex.Message}>"
                    });
                }
            }

            // Sort by name for consistent output
            members.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

            return members;
        }

        private MemberData CreateMemberInfo(string name, Type declaredType, object value)
        {
            var actualType = value?.GetType() ?? declaredType;
            var isPrimitive = IsPrimitiveType(actualType);

            var member = new MemberData
            {
                Name = name,
                Type = GetTypeName(actualType),
                AssemblyName = actualType.Assembly.GetName().Name,
                IsPrimitive = isPrimitive
            };

            if (value == null)
            {
                member.Value = null;
                member.HandleId = null;
            }
            else if (isPrimitive)
            {
                member.Value = GetPrimitiveValue(value);
                member.HandleId = null;
            }
            else
            {
                member.Value = GetSafeToString(value);
                member.HandleId = _registry.Register(value).ToString();
            }

            return member;
        }

        private bool IsCollection(object obj, out CollectionInfo info)
        {
            info = null;

            if (obj == null) return false;

            var type = obj.GetType();

            // Check if it's IEnumerable (but not string)
            if (obj is string) return false;
            if (!(obj is IEnumerable enumerable)) return false;

            // Determine element type
            Type elementType = typeof(object);
            
            if (type.IsArray)
            {
                elementType = type.GetElementType();
            }
            else if (type.IsGenericType)
            {
                var genericArgs = type.GetGenericArguments();
                if (genericArgs.Length > 0)
                {
                    elementType = genericArgs[genericArgs.Length - 1]; // Last arg for dictionaries is value type
                }
            }

            // Collect all elements
            var elements = new List<CollectionElement>();
            int index = 0;

            try
            {
                foreach (var item in enumerable)
                {
                    var element = new CollectionElement
                    {
                        Index = index
                    };

                    if (item == null)
                    {
                        element.HandleId = null;
                        element.Type = GetTypeName(elementType);
                        element.Value = null;
                    }
                    else if (IsPrimitiveType(item.GetType()))
                    {
                        element.HandleId = null;
                        element.Type = GetTypeName(item.GetType());
                        element.Value = GetPrimitiveValue(item);
                    }
                    else
                    {
                        element.HandleId = _registry.Register(item).ToString();
                        element.Type = GetTypeName(item.GetType());
                        element.Value = GetSafeToString(item);
                    }

                    elements.Add(element);
                    index++;
                }
            }
            catch (Exception ex)
            {
                Entry.LogError($"Error enumerating collection: {ex.Message}");
            }

            info = new CollectionInfo
            {
                IsCollection = true,
                Count = elements.Count,
                ElementType = GetTypeName(elementType),
                Elements = elements
            };

            return true;
        }

        private bool IsPrimitiveType(Type type)
        {
            if (type == null) return false;
            if (PrimitiveTypes.Contains(type)) return true;
            if (type.IsEnum) return true;
            if (Nullable.GetUnderlyingType(type) != null)
            {
                return IsPrimitiveType(Nullable.GetUnderlyingType(type));
            }
            return false;
        }

        private object GetPrimitiveValue(object value)
        {
            if (value == null) return null;

            var type = value.GetType();

            if (type.IsEnum)
            {
                return value.ToString();
            }

            if (type == typeof(DateTime))
            {
                return ((DateTime)value).ToString("O");
            }

            if (type == typeof(TimeSpan))
            {
                return value.ToString();
            }

            if (type == typeof(Guid))
            {
                return value.ToString();
            }

            return value;
        }

        private string GetTypeName(Type type)
        {
            if (type == null) return "null";
            
            // Handle generic types
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                var genericArgs = type.GetGenericArguments();
                var baseName = type.FullName?.Split('`')[0] ?? type.Name.Split('`')[0];
                var argNames = string.Join(", ", genericArgs.Select(GetTypeName));
                return $"{baseName}<{argNames}>";
            }

            return type.FullName ?? type.Name;
        }

        private string GetSafeToString(object obj)
        {
            if (obj == null) return null;

            try
            {
                var str = obj.ToString();
                // Limit length to avoid huge strings
                if (str != null && str.Length > 500)
                {
                    return str.Substring(0, 500) + "...";
                }
                return str;
            }
            catch
            {
                return obj.GetType().Name;
            }
        }
    }
}
