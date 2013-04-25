﻿using NUnit.Framework;

namespace SimpleRedis.Tests
{
    [TestFixture]
    public class BasicTests
    {
        [Test]
        public void Connect()
        {
            // note: defaults to 127.0.0.1:6379
            using(dynamic client = new RedisClient())
            { 
            }
        }

        [Test]
        public void GetSet()
        {
            using (dynamic client = new RedisClient())
            {
                client.del("getset"); // wipe
                client.set("getset", "def"); // assign
                string val = client.get("getset"); // fetch
                Assert.AreEqual("def", val);
            }
        }

        [Test]
        public void Counters()
        {
            using (dynamic client = new RedisClient())
            {
                client.del("counter"); // note: missing counts as 0
                int a = client.incr("counter"); // 0+1: should be 1
                int b = client.incrby("counter", 5); // 1+5, should be 6
                int c = client.decr("counter"); // 6-1: should be 5

                Assert.AreEqual(1, a, "a");
                Assert.AreEqual(6, b, "b");
                Assert.AreEqual(5, c, "c");
            }
        }

        [Test]
        public void DeleteResult()
        {
            using (dynamic client = new RedisClient())
            {
                client.set("delete", "some val"); // give it a value initially
                bool first = client.del("delete"); // was deleted: true
                bool second = client.del("delete"); // no longer existed: false

                Assert.IsTrue(first);
                Assert.IsFalse(second);
            }
        }

        [Test, ExpectedException(typeof(RedisException), ExpectedMessage = "ERR value is not an integer or out of range")]
        public void DoomedToFail()
        {
            using (dynamic client = new RedisClient())
            {
                client.set("fail_incr", "some val"); // give it a value initially
                client.incr("fail_incr"); // what is numeric "some val" + 1 ?
            }
        }
    }
}
