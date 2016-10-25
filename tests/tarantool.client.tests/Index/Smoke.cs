﻿using System;
using System.Threading.Tasks;

using Xunit;

using ProGaudi.Tarantool.Client.Model;
using ProGaudi.Tarantool.Client.Model.Responses;
using ProGaudi.Tarantool.Client.Model.UpdateOperations;
using Shouldly;

namespace ProGaudi.Tarantool.Client.Tests.Index
{
    public class Smoke
    {
        [Fact]
        public async Task HashIndexMethods()
        {
            const string spaceName = "primary_only_index";
            var tarantoolClient = await Client.Box.Connect("127.0.0.1:3301");

            var schema = tarantoolClient.GetSchema();

            var space = await schema.GetSpace(spaceName);

            var index = await space.GetIndex("primary");

            try
            {
                await index.Insert(TarantoolTuple.Create(2, "Music"));
            }
            catch (ArgumentException)
            {
                await index.Delete<TarantoolTuple<int>, TarantoolTuple<int, string, double>>(TarantoolTuple.Create(2));
                await index.Insert(TarantoolTuple.Create(2, "Music"));
            }

            await index.Select<TarantoolTuple<uint>, TarantoolTuple<int, string>>(TarantoolTuple.Create(1029u));
            await index.Replace(TarantoolTuple.Create(2, "Car", -245.3));
            await index.Update<TarantoolTuple<int, string, double>, TarantoolTuple<int>>(
                TarantoolTuple.Create(2),
                new UpdateOperation[] { UpdateOperation.CreateAddition(100, 2) });

            await index.Upsert(TarantoolTuple.Create(5u), new UpdateOperation[] { UpdateOperation.CreateAssign(2, 2) });
            await index.Upsert(TarantoolTuple.Create(5u), new UpdateOperation[] { UpdateOperation.CreateAddition(-2, 2) });

            await index.Select<TarantoolTuple<uint>, TarantoolTuple<int, int, int>>(TarantoolTuple.Create(5u));
        }

        [Fact]
        public async Task TreeIndexMethods()
        {
            const string spaceName = "space_TreeIndexMethods";
            var tarantoolClient = await Client.Box.Connect("127.0.0.1:3301");

            var schema = tarantoolClient.GetSchema();

            var space = await schema.GetSpace(spaceName);

            var index = await space.GetIndex("treeIndex");

            var min2 = await index.Min<TarantoolTuple<int, int, int>, TarantoolTuple<int>>(TarantoolTuple.Create(3));
            min2.ShouldBe(TarantoolTuple.Create(3, 2, 3));
            var min = await index.Min<TarantoolTuple<int, string, double>>();
            min.ShouldBe(TarantoolTuple.Create(1, "asdf", 10.1));

            var max = await index.Max<TarantoolTuple<int, int, int>>();
            max.ShouldBe(min2);
            var max2 = await index.Max<TarantoolTuple<int, string, double>, TarantoolTuple<int>>(TarantoolTuple.Create(1));
            max2.ShouldBe(min);
        }
    }
}