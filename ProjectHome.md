This client is primarily intended to show the use of DynamicObject and how we can create rich APIs simply.

Note that this is **not** intended to be used as a production utility (although you're welcome to try - I just haven't tested it in every possible scenario); for that I recommend [BookSleeve](http://nuget.org/packages/BookSleeve/) or [ServiceStack.Redis](http://nuget.org/packages/ServiceStack.Redis/).

This code illustrates how to write a fully dynamic client in C# / .NET which can behave like:

```
            using (dynamic client = new RedisClient())
            {
                client.del("list");
                client.rpush("list", "item 1");
                client.rpush("list", "item 2");

                string a = client.lpop("list");
                string b = client.lpop("list");
                string c = client.lpop("list");

                client.rpush("list", "item 3");
                string d = client.lpop("list");

                Assert.AreEqual(a, "item 1");
                Assert.AreEqual(b, "item 2");
                Assert.IsNull(c);
                Assert.AreEqual(d, "item 3");
            }
```