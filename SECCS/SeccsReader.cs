﻿using SECCS.Exceptions;
using System;
using System.Runtime.Serialization;

namespace SECCS
{
    public sealed class SeccsReader<TReader> : IBufferReader<TReader>
    {
        public FormatCollection<IReadFormat<TReader>> Formats { get; } = new FormatCollection<IReadFormat<TReader>>();

        public SeccsReader()
        {
            Formats.Discover();
        }

        internal SeccsReader(FormatCollection<IReadFormat<TReader>> formats)
        {
            this.Formats = formats ?? throw new ArgumentNullException(nameof(formats));
        }

        public object Deserialize(TReader reader, Type objType, ReadFormatContext<TReader>? context = null)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            if (objType == null)
                throw new ArgumentNullException(nameof(objType));

            var format = Formats.GetFor(objType);
            context = context ?? new ReadFormatContext<TReader>(this, reader, "");

            var obj = format.Read(objType, context.Value);

            if (obj is IDeserializationCallback callback)
                callback.OnDeserialization(this);

            return obj;
        }

        public T Deserialize<T>(TReader reader, ReadFormatContext<TReader>? context = null)
            => (T)Deserialize(reader, typeof(T), context);
    }
}
