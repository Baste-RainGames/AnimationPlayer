using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class AssemblyHelper {

    private static bool hasErroredThisReload;
    private static IReadOnlyList<Type> allTypesCache;

    public static IEnumerable<Type> GetAllTypes(bool allowCache = true) {
        if(!allowCache)
            return GetTypesFrom(AppDomain.CurrentDomain.GetAssemblies());

        if (allTypesCache == null || allTypesCache.Count == 0)
            allTypesCache = GetTypesFrom(AppDomain.CurrentDomain.GetAssemblies()).ToList();

        return allTypesCache;
    }

    public static IEnumerable<Type> GetTypesFrom(IEnumerable<Assembly> assemblies) {
        foreach (var assembly in assemblies) {
            Type[] types = null;
            try {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException rtle) {
                if (!hasErroredThisReload) {
                    // If this errors, it might be that https://issuetracker.unity3d.com/issues/reflectiontypeloadexception-is-thrown-when-using-reflection-to-get-the-types-from-the-accessibility-assembly
                    // has been reintroduced. It's supposed to be fixed in 2018.3.12.
                    Debug.LogError($"Can't load types from assembly {assembly.GetName().Name}. Exceptions follow");
                    Debug.LogException(rtle);
                    foreach (var exception in rtle.LoaderExceptions) {
                        Debug.LogException(exception);
                    }
                    hasErroredThisReload = true;
                }

                continue;
            }

            foreach (var type in types) {
                yield return type;
            }
        }
    }
}