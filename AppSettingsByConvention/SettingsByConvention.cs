﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using AppSettingsByConventionTests.ProxyBuilding;

namespace AppSettingsByConvention
{
    /// <summary>
    /// Reads settings from the appSettings section of your configuration by convention
    /// The keys should follow the pattern CLASSNAME.PROPERTYNAME
    /// All properties on your config object need to appear in your configuration
    /// 
    /// To read a connection string, use AppSettingsByConvention.IConnectionString as a property,
    /// the same naming rules apply.
    /// 
    /// Example use:
    ///   SettingsByConvention.ForInterface<IConfiguration>()
    ///   SettingsByConvention.ForClass<Configuration>() 
    ///   SettingsByConvention.For(typeof(IConfigiration)) 
    ///   SettingsByConvention.For(typeof(Configiration))
    ///  
    /// Inversion of Control-setup: Since the values never change after application starts,
    /// I recommend that you register as a Singleton.
    /// </summary>
    public static class SettingsByConvention
    {
        public static readonly Dictionary<Type, Func<string, object>> ParserMappings;
        public static char ListSeparator { get; set; }
        public static Func<IParser> ParserFactory { get; set; }

        static SettingsByConvention()
        {
            ListSeparator = ';';
            Func<string, string[]> stringListParser = input => input.Split(ListSeparator);
            ParserMappings = new Dictionary<Type, Func<string, object>>
                {
                    {typeof(string), input => input},
                    {typeof(int), input => int.Parse(input)},
                    {typeof(bool), input => bool.Parse(input)},
                    {typeof(string[]), stringListParser},
                    {typeof(List<string>), input => stringListParser(input).ToList()}
                };
            ParserFactory = () => new Parser(ParserMappings);
        }

        public static object For(Type type)
        {
            MethodInfo createMethod;
            if (type.IsInterface)
            {
                createMethod = typeof(SettingsByConvention).GetMethod("ForInterface").MakeGenericMethod(type);
            }
            else if (type.IsClass && type.GetConstructor(Type.EmptyTypes) != null)
            {
                createMethod = typeof(SettingsByConvention).GetMethod("ForClass").MakeGenericMethod(type);
            }
            else
            {
                throw new InvalidOperationException($"Type {type} is neither an interface nor a class with an empty constructor.");
            }
            try
            {
                //null, null = static method that is parameterless
                return createMethod.Invoke(null, null);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        public static TClassWithDefaultConstructor ForClass<TClassWithDefaultConstructor>() where TClassWithDefaultConstructor : class, new()
        {
            return new AppSettingsIntoClassLoader<TClassWithDefaultConstructor>(GetValueProviders<TClassWithDefaultConstructor>()).Create();
        }

        public static TInterface ForInterface<TInterface>() where TInterface : class
        {
            var configurationType = typeof(TInterface);
            if (configurationType.IsInterface == false)
            {
                throw new InvalidOperationException($"{typeof(TInterface)} is not an interface");
            }
            return (TInterface)For(configurationType.ImplementClassWithProperties());
        }

        private static IEnumerable<IValueProvider> GetValueProviders<T>() where T : class
        {
            var appSettingValueParser = ParserFactory();
            yield return new AppSettingValueProvider<T>(appSettingValueParser);
            yield return new ConnectionStringValueProvider<T>();
        }
    }
}
