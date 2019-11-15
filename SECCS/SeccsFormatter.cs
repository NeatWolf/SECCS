﻿using AgileObjects.ReadableExpressions;
using SECCS.Attributes;
using SECCS.DefaultFormats;
using SECCS.Exceptions;
using SECCS.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;

namespace SECCS
{
    using static Expression;

    public class SeccsFormatter<TBuffer>
    {
        private const byte Magic = 243;
        private const byte MagicWithSignature = 244;

        private static readonly ITypeFormat[] DefaultFormats;

        /// <summary>
        /// Collection of registered type formats.
        /// </summary>
        public TypeFormatCollection<TBuffer> Formats { get; }

        private readonly IDictionary<Type, Func<TBuffer, object>> Deserializers = new Dictionary<Type, Func<TBuffer, object>>();
        private readonly IDictionary<Type, Action<TBuffer, object>> Serializers = new Dictionary<Type, Action<TBuffer, object>>();

        private readonly SeccsOptions Options;

        static SeccsFormatter()
        {
            DefaultFormats = typeof(ITypeFormat).Assembly
                .GetTypes()
                .Where(o => typeof(ITypeFormat).IsAssignableFrom(o) && o.Namespace == typeof(ValueTupleFormat).Namespace)
                .OrderByDescending(o => o.GetCustomAttribute<PriorityAttribute>()?.Priority ?? 0)
                .Select(o => (ITypeFormat)Activator.CreateInstance(o))
                .ToArray();
        }

        /// <summary>
        /// Instantiates a <see cref="SeccsFormatter{TBuffer}"/> with no additional formatters and default options.
        /// </summary>
        /// <param name="options">The options object, or null for default</param>
        public SeccsFormatter(SeccsOptions options = null) : this(Enumerable.Empty<ITypeFormat>(), options)
        {
        }

        /// <summary>
        /// Instantiates a <see cref="SeccsFormatter{TBuffer}"/> with additional formatters and default options.
        /// </summary>
        /// <param name="formats">The additional formatters to be used</param>
        /// <param name="options">The options object, or null for default</param>
        public SeccsFormatter(IEnumerable<ITypeFormat> formats, SeccsOptions options = null)
        {
            this.Formats = new TypeFormatCollection<TBuffer>();
            this.Options = options ?? new SeccsOptions();

            Formats.Register(DefaultFormats);
            Formats.Register(formats);
            Formats.SortByPriority();
        }

        /// <summary>
        /// Serializes <paramref name="obj"/> into <typeparamref name="TBuffer"/> using type formats.
        /// </summary>
        /// <typeparam name="T">The type of the object to serialize</typeparam>
        /// <param name="buffer">The buffer to serialize the object into</param>
        /// <param name="obj">The object to serialize</param>
        public void Serialize<T>(TBuffer buffer, T obj)
            => Serialize(buffer, obj, typeof(T));

        /// <summary>
        /// Serializes an <paramref name="obj"/> of type <paramref name="type"/> into <paramref name="buffer"/>
        /// </summary>
        /// <param name="buffer">The buffer to serialize the object into</param>
        /// <param name="obj">The object to serialize</param>
        /// <param name="type">The object's type, or null to get it from <paramref name="obj"/></param>
        public void Serialize(TBuffer buffer, object obj, Type type = null)
        {
            type = type ?? obj?.GetType() ?? throw new ArgumentNullException("obj is null and no type has been specified");

            if (!Serializers.TryGetValue(type, out var ser))
            {
                var bufferParam = Parameter(typeof(TBuffer), "_buffer");
                var objParam = Parameter(typeof(object), "_obj");
                var exprs = new List<Expression>();

                if (Options.WriteHeader)
                {
                    //Write 243, or 244 if Options.WriteStructureSignature is on
                    exprs.Add(Formats.Get(typeof(byte)).Serialize(new FormatContextWithValue(Formats, typeof(byte), typeof(TBuffer), bufferParam, Constant(Options.WriteStructureSignature ? MagicWithSignature : Magic))));

                    if (Options.WriteStructureSignature)
                    {
                        var hash = ClassSignature.Get(type);
                        exprs.Add(Formats.Get(typeof(string)).Serialize(new FormatContextWithValue(Formats, typeof(string), typeof(TBuffer), bufferParam, Constant(hash))));
                    }
                }

                exprs.Add(Formats.Get(type).Serialize(new FormatContextWithValue(Formats, type, typeof(TBuffer), bufferParam, objParam)));

                var lambda = Lambda<Action<TBuffer, object>>(Block(exprs), bufferParam, objParam);

#if DEBUG
                //Debug.WriteLine($"Serializer for {type.FullName}:\n{lambda.ToReadableString()}");
#endif

                Serializers[type] = ser = lambda.Compile();
            }

            ser(buffer, obj);
        }

        /// <summary>
        /// Reads a <typeparamref name="T"/> from a <paramref name="buffer"/> of type <typeparamref name="TBuffer"/>.
        /// </summary>
        /// <typeparam name="T">The type of the object to read</typeparam>
        /// <param name="buffer">The buffer to read from</param>
        public T Deserialize<T>(TBuffer buffer)
            => (T)Deserialize(buffer, typeof(T));

        /// <summary>
        /// Reads a <typeparamref name="T"/> from a <paramref name="buffer"/> of type <typeparamref name="TBuffer"/>.
        /// The type of <typeparamref name="T"/> can be inferred from the first parameter, which allows you to
        /// pass in an anonymous object with the fields that you want to deserialize. For example:
        /// <code>
        /// DeserializeAnonymousObject(new { Foo = "asd" }, buffer);
        /// </code>
        /// </summary>
        /// <typeparam name="T">The type of the object to read</typeparam>
        /// <param name="anonymousTypeObject">The anonymous object whose type will be inferred</param>
        /// <param name="buffer">The buffer to read from</param>
        public T DeserializeAnonymousObject<T>(T anonymousTypeObject, TBuffer buffer)
        {
            _ = anonymousTypeObject; //Supress "parameter not used"
            return Deserialize<T>(buffer);
        }

        /// <summary>
        /// Reads an object of type <paramref name="type"/> from <paramref name="buffer"/>.
        /// </summary>
        /// <param name="buffer">The buffer to read from</param>
        /// <param name="type">The type of the object to read</param>
        public object Deserialize(TBuffer buffer, Type type)
        {
            if (!Deserializers.TryGetValue(type, out var des))
            {
                var exprs = new List<Expression>();
                var bufferParam = Parameter(typeof(TBuffer), "_buffer");

                if (Options.CheckHeader)
                {
                    var magicVar = Variable(typeof(byte), "_magic");
                    var hash = ClassSignature.Get(type);

                    /*
                     byte magic = Read<byte>();
                     if (magic == 244)
                     {
                         if (Read<string>() != "HASH")
                         {
                             if (Options.CheckStructureSignature)
                                 throw new Exception();
                         }
                     }
                     else if (magic != 243)
                     {
                         throw new Exception();
                     }
                     */
                    exprs.Add(Block(new[] { magicVar },
                        Assign(magicVar, ReadG<byte>()),
                        IfThenElse(
                            Equal(magicVar, Constant(MagicWithSignature)),
                            IfThen(NotEqual(ReadG<string>(), Constant(hash)), Options.CheckStructureSignature ? InvalidHeaderException.Throw("Class structure signature mismatch") : Block()),
                            IfThen(NotEqual(magicVar, Constant(Magic)), InvalidHeaderException.Throw("Invalid magic number")))));
                }

                exprs.Add(Read(type));

                var lambda = Lambda<Func<TBuffer, object>>(Block(exprs), bufferParam);

#if DEBUG
                Debug.WriteLine($"Deserializer for {type.FullName}:\n{lambda.ToReadableString()}");
#endif

                Deserializers[type] = des = lambda.Compile();

                Expression ReadG<T>() => Read(typeof(T));
                Expression Read(Type t) => Formats.Get(t)?.Deserialize(new FormatContext(Formats, t, typeof(TBuffer), bufferParam)) ?? throw new InvalidOperationException("Cannot deserialize type " + t.FullName);
            }

            return des(buffer);
        }
    }
}
