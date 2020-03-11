﻿using System;

namespace SECCS
{
    public interface IReadFormat<TReader> : IFormat
    {
        object Read(TReader reader, Type type, ReadFormatContext<TReader> context);
    }

    public abstract class ReadFormat<T, TReader> : IReadFormat<TReader>
    {
        bool IFormat.CanFormat(Type type) => type == typeof(T);
        object IReadFormat<TReader>.Read(TReader reader, Type type, ReadFormatContext<TReader> context) => Read(reader, context)!;

        public abstract T Read(TReader reader, ReadFormatContext<TReader> context);
    }

    public delegate T ReadDelegate<T, TReader>(TReader reader);

    public sealed class DelegateReadFormat<T, TReader> : ReadFormat<T, TReader>
    {
        private readonly ReadDelegate<T, TReader> Reader;

        public DelegateReadFormat(ReadDelegate<T, TReader> readFunc)
        {
            this.Reader = readFunc ?? throw new ArgumentNullException(nameof(readFunc));
        }

        public override T Read(TReader reader, ReadFormatContext<TReader> context) => Reader(reader);
    }
}
