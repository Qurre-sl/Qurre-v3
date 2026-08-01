using System;
using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;

namespace Qurre.API.Helpers;

    /// <summary>
    /// Универсальный набор методов для доступа к private/protected полям,
    /// свойствам, методам и конструкторам через рефлексию, с кэшированием
    /// найденных MethodInfo/FieldInfo/PropertyInfo и типизированных делегатов.
    /// Использует HarmonyLib.AccessTools как основу — он уже сам кэширует
    /// часть данных и умеет искать non-public члены.
    /// </summary>
    public static class ReflectionHelper
    {
        private const BindingFlags AllInstance =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
 
        private const BindingFlags AllStatic =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
 
        // ---------- Кэши, чтобы не искать одно и то же по многу раз ----------
        private static readonly ConcurrentDictionary<(Type, string), FieldInfo> FieldCache = new();
        private static readonly ConcurrentDictionary<(Type, string), PropertyInfo> PropertyCache = new();
        private static readonly ConcurrentDictionary<(Type, string, int), MethodInfo> MethodCache = new();
        private static readonly ConcurrentDictionary<(Type, string), Type> NestedTypeCache = new();
 
        // =====================================================================
        // 1. ПОЛЯ (instance) — типизированный быстрый доступ через FieldRef
        // =====================================================================
 
        /// <summary>
        /// Возвращает делегат для чтения/записи приватного instance-поля.
        /// Используйте, когда точно знаете тип TOwner (класс поля доступен)
        /// и тип TField (тип самого поля публичный/доступный).
        ///
        /// Пример:
        ///   var rbRef = ReflectionHelper.FieldRef&lt;BodyArmorPickup, Rigidbody&gt;("_rb");
        ///   Rigidbody rb = rbRef(instance);      // чтение
        ///   rbRef(instance) = newRigidbody;      // запись (это ref!)
        /// </summary>
        public static AccessTools.FieldRef<TOwner, TField> FieldRef<TOwner, TField>(string fieldName)
            => AccessTools.FieldRefAccess<TOwner, TField>(fieldName);
 
        /// <summary>
        /// Получить значение приватного поля через "медленную" рефлексию (object).
        /// Используйте, когда тип поля/владельца недоступен для generic-параметра
        /// (например, приватный вложенный класс) или нужен разовый доступ без кэша делегата.
        ///
        /// Пример:
        ///   object value = ReflectionHelper.GetField(instance, "_someField");
        /// </summary>
        public static object GetField(object instance, string fieldName)
        {
            var field = FieldCache.GetOrAdd((instance.GetType(), fieldName),
                key => AccessTools.Field(key.Item1, key.Item2)
                       ?? throw new MissingFieldException($"Field '{fieldName}' not found on {instance.GetType()}"));
            return field.GetValue(instance);
        }
 
        /// <summary>
        /// Установить значение приватного поля (когда типы неизвестны на этапе компиляции).
        ///
        /// Пример:
        ///   ReflectionHelper.SetField(instance, "_released", true);
        /// </summary>
        public static void SetField(object instance, string fieldName, object value)
        {
            var field = FieldCache.GetOrAdd((instance.GetType(), fieldName),
                key => AccessTools.Field(key.Item1, key.Item2)
                       ?? throw new MissingFieldException($"Field '{fieldName}' not found on {instance.GetType()}"));
            field.SetValue(instance, value);
            
        }
        
        public static FieldInfo GetFieldInfo(Type ownerType, string fieldName)
        {
            return FieldCache.GetOrAdd((ownerType, fieldName),
                key => AccessTools.Field(key.Item1, key.Item2)
                       ?? throw new MissingFieldException($"Field '{fieldName}' not found on {ownerType}"));
        }
        
        public static Action<TValue> StaticPropertySetter<TValue>(Type ownerType, string propertyName)
        {
            var setter = AccessTools.PropertySetter(ownerType, propertyName)
                         ?? throw new MissingMemberException($"Static property setter '{propertyName}' not found on {ownerType}");
            return AccessTools.MethodDelegate<Action<TValue>>(setter);
        }
 
        // =====================================================================
        // 2. СТАТИЧЕСКИЕ ПОЛЯ
        // =====================================================================
 
        /// <summary>
        /// Получить значение приватного static-поля без явного instance.
        ///
        /// Пример:
        ///   var candidates = ReflectionHelper.GetStaticField&lt;List&lt;ReferenceHub&gt;&gt;(typeof(HumanSpawner), "Candidates");
        /// </summary>
        public static T GetStaticField<T>(Type ownerType, string fieldName)
        {
            var field = FieldCache.GetOrAdd((ownerType, fieldName),
                key => AccessTools.Field(key.Item1, key.Item2)
                       ?? throw new MissingFieldException($"Static field '{fieldName}' not found on {ownerType}"));
            return (T)field.GetValue(null);
        }
        
        public static MethodInfo GetMethod(Type ownerType, string methodName, int argCount = -1)
        {
            return MethodCache.GetOrAdd((ownerType, methodName, argCount), key =>
            {
                if (argCount == -1)
                {
                    return AccessTools.Method(ownerType, methodName)
                           ?? throw new MissingMethodException($"Method '{methodName}' not found on {ownerType}");
                }

                return FindMethod(ownerType, methodName, argCount);
            });
        }
 
        public static void SetStaticField(Type ownerType, string fieldName, object value)
        {
            var field = FieldCache.GetOrAdd((ownerType, fieldName),
                key => AccessTools.Field(key.Item1, key.Item2)
                       ?? throw new MissingFieldException($"Static field '{fieldName}' not found on {ownerType}"));
            field.SetValue(null, value);
        }
 
        // =====================================================================
        // 3. СВОЙСТВА (properties) — приватный геттер/сеттер
        // =====================================================================
 
        /// <summary>
        /// Быстрый типизированный делегат для чтения приватного свойства (get).
        ///
        /// Пример:
        ///   var isAffected = ReflectionHelper.PropertyGetter&lt;BodyArmorPickup, bool&gt;("IsAffected");
        ///   bool value = isAffected(instance);
        /// </summary>
        public static Func<TOwner, TValue> PropertyGetter<TOwner, TValue>(string propertyName)
        {
            var getter = AccessTools.PropertyGetter(typeof(TOwner), propertyName)
                ?? throw new MissingMemberException($"Property getter '{propertyName}' not found on {typeof(TOwner)}");
            return AccessTools.MethodDelegate<Func<TOwner, TValue>>(getter);
        }
 
        /// <summary>
        /// Быстрый типизированный делегат для записи приватного свойства (set).
        ///
        /// Пример:
        ///   var setDamage = ReflectionHelper.PropertySetter&lt;SomeClass, float&gt;("Damage");
        ///   setDamage(instance, 15f);
        /// </summary>
        public static Action<TOwner, TValue> PropertySetter<TOwner, TValue>(string propertyName)
        {
            var setter = AccessTools.PropertySetter(typeof(TOwner), propertyName)
                ?? throw new MissingMemberException($"Property setter '{propertyName}' not found on {typeof(TOwner)}");
            return AccessTools.MethodDelegate<Action<TOwner, TValue>>(setter);
        }
 
        /// <summary>
        /// Универсальный доступ к свойству через object — когда тип неизвестен на этапе компиляции
        /// (например, свойство приватного вложенного класса).
        ///
        /// Пример:
        ///   object value = ReflectionHelper.GetProperty(roleHistoryInstance, "History");
        /// </summary>
        public static object GetProperty(object instance, string propertyName)
        {
            var property = PropertyCache.GetOrAdd((instance.GetType(), propertyName),
                key => AccessTools.Property(key.Item1, key.Item2)
                       ?? throw new MissingMemberException($"Property '{propertyName}' not found on {instance.GetType()}"));
            return property.GetValue(instance);
        }
 
        // =====================================================================
        // 4. МЕТОДЫ (methods) — вызов приватных/protected методов
        // =====================================================================
 
        /// <summary>
        /// Вызвать приватный instance-метод без возврата значения (void), с любым числом аргументов.
        /// Метод ищется один раз и кэшируется по (тип, имя, кол-во_аргументов).
        ///
        /// Пример:
        ///   ReflectionHelper.InvokeMethod(instance, "UpdatePositionServer");
        ///   ReflectionHelper.InvokeMethod(instance, "SomeMethod", arg1, arg2);
        /// </summary>
        public static void InvokeMethod(object instance, string methodName, params object[] args)
        {
            var method = MethodCache.GetOrAdd((instance.GetType(), methodName, args.Length),
                key => FindMethod(key.Item1, key.Item2, key.Item3));
            method.Invoke(instance, args);
        }
 
        /// <summary>
        /// Вызвать приватный instance-метод и получить результат.
        ///
        /// Пример:
        ///   float result = ReflectionHelper.InvokeMethod&lt;float&gt;(instance, "CalculateDamage", hitbox);
        /// </summary>
        public static T InvokeMethod<T>(object instance, string methodName, params object[] args)
        {
            var method = MethodCache.GetOrAdd((instance.GetType(), methodName, args.Length),
                key => FindMethod(key.Item1, key.Item2, key.Item3));
            return (T)method.Invoke(instance, args);
        }
 
        /// <summary>
        /// Вызвать приватный static-метод.
        ///
        /// Пример:
        ///   ReflectionHelper.InvokeStaticMethod(typeof(HumanSpawner), "Invoke", RoleTypeId.ClassD);
        /// </summary>
        public static void InvokeStaticMethod(Type ownerType, string methodName, params object[] args)
        {
            var method = MethodCache.GetOrAdd((ownerType, methodName, args.Length),
                key => FindMethod(key.Item1, key.Item2, key.Item3));
            method.Invoke(null, args);
        }
 
        public static T InvokeStaticMethod<T>(Type ownerType, string methodName, params object[] args)
        {
            var method = MethodCache.GetOrAdd((ownerType, methodName, args.Length),
                key => FindMethod(key.Item1, key.Item2, key.Item3));
            return (T)method.Invoke(null, args);
        }
 
        private static MethodInfo FindMethod(Type type, string name, int argCount)
        {
            // Ищем среди всех методов с нужным именем метод с подходящим числом параметров.
            // Если у метода одна перегрузка — этого достаточно. Если несколько с одинаковым
            // числом аргументов — нужно использовать AccessTools.Method с явным указанием типов.
            foreach (var m in type.GetMethods(AllInstance | AllStatic))
                if (m.Name == name && m.GetParameters().Length == argCount)
                    return m;
 
            throw new MissingMethodException($"Method '{name}' with {argCount} args not found on {type}");
        }
 
        /// <summary>
        /// Вызвать метод с явным указанием типов параметров — нужно, если есть
        /// несколько перегрузок с одинаковым числом аргументов.
        ///
        /// Пример:
        ///   ReflectionHelper.InvokeMethodExplicit(instance, "ProcessPlayer",
        ///       new[] { typeof(ReferenceHub), typeof(float) }, hub, 5f);
        /// </summary>
        public static object InvokeMethodExplicit(object instance, string methodName, Type[] paramTypes, params object[] args)
        {
            var method = AccessTools.Method(instance.GetType(), methodName, paramTypes)
                ?? throw new MissingMethodException($"Method '{methodName}' not found on {instance.GetType()}");
            return method.Invoke(instance, args);
        }
 
        // =====================================================================
        // 5. ПРИВАТНЫЕ ВЛОЖЕННЫЕ ТИПЫ И СОЗДАНИЕ ЭКЗЕМПЛЯРОВ
        // =====================================================================
 
        /// <summary>
        /// Получить Type приватного вложенного класса (например HumanSpawner.RoleHistory),
        /// который нельзя написать в коде через typeof() напрямую.
        ///
        /// Пример:
        ///   Type roleHistoryType = ReflectionHelper.GetNestedType(typeof(HumanSpawner), "RoleHistory");
        /// </summary>
        public static Type GetNestedType(Type outerType, string nestedTypeName)
        {
            return NestedTypeCache.GetOrAdd((outerType, nestedTypeName),
                key => AccessTools.Inner(key.Item1, key.Item2)
                       ?? throw new TypeLoadException($"Nested type '{nestedTypeName}' not found in {outerType}"));
        }
 
        /// <summary>
        /// Создать экземпляр приватного (в т.ч. вложенного) класса, включая случаи
        /// с приватным/отсутствующим публичным конструктором.
        ///
        /// Пример:
        ///   object instance = ReflectionHelper.CreateInstance(roleHistoryType);
        /// </summary>
        public static object CreateInstance(Type type, params object[] args)
        {
            return args.Length == 0
                ? AccessTools.CreateInstance(type)
                : Activator.CreateInstance(type, AllInstance, null, args, null);
        }
 
        // =====================================================================
        // 6. NON-GENERIC СЛОВАРИ / КОЛЛЕКЦИИ С ПРИВАТНЫМ TValue
        // =====================================================================
 
        /// <summary>
        /// Обёртка-подсказка: если поле — Dictionary&lt;TKey, TPrivateType&gt;, где TPrivateType
        /// недоступен как generic-параметр, кастуйте результат к System.Collections.IDictionary,
        /// а не к Dictionary&lt;TKey, TPrivateType&gt;. Индексатор и Contains() работают как обычно,
        /// просто значения будут типа object.
        ///
        /// Пример:
        ///   var dict = (System.Collections.IDictionary)ReflectionHelper.GetStaticField&lt;object&gt;(typeof(HumanSpawner), "History");
        ///   object entry = dict["someKey"];
        /// </summary>
        public static class Notes { } // маркер-класс просто для группировки комментария выше в IntelliSense
    }
