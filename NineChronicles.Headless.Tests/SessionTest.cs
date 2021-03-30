using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace NineChronicles.Headless.Tests
{
    public abstract class SessionTest
    {
        private readonly ISession _session;

        protected SessionTest(ISession session)
        {
            _session = session;
        }

        [Fact]
        public void Clear()
        {
            _session.Set("foo", new byte[] { 0xbe, 0xef });
            Assert.NotEmpty(_session.Keys);
            _session.Clear();
            Assert.Empty(_session.Keys);
        }

        [Fact]
        public void Remove()
        {
            const string foo = "foo", bar = "bar", session = "session";
            _session.Set(foo, new byte[] { 0xbe, 0xef });
            _session.Set(bar, new byte[] { 0xbe, 0xef });
            _session.Set(session, new byte[] { 0xab, 0xcd });
            Assert.Contains(foo, _session.Keys);
            Assert.Contains(bar, _session.Keys);
            Assert.Contains(session, _session.Keys);
            _session.Remove(foo);
            Assert.DoesNotContain(foo, _session.Keys);
            Assert.Contains(bar, _session.Keys);
            Assert.Contains(session, _session.Keys);
            _session.Remove(bar);
            Assert.DoesNotContain(foo, _session.Keys);
            Assert.DoesNotContain(bar, _session.Keys);
            Assert.Contains(session, _session.Keys);
            _session.Remove(session);
            Assert.DoesNotContain(foo, _session.Keys);
            Assert.DoesNotContain(bar, _session.Keys);
            Assert.DoesNotContain(session, _session.Keys);
        }

        [Theory]
        [InlineData("foo", new byte[] { 0x00 })]
        [InlineData("bar", new byte[] { 0x01 })]
        public void Set(string key, byte[] value)
        {
            _session.Set(key, value);
            Assert.True(_session.TryGetValue(key, out byte[] storedValue));
            Assert.Equal(value, storedValue);
        }

        [Fact]
        public void TryGetValue()
        {
            const string foo = "foo";
            Assert.False(_session.TryGetValue(foo, out byte[] _));

            var value = new byte[] { 0xbe, 0xef };
            _session.Set(foo, value);
            Assert.True(_session.TryGetValue(foo, out byte[] storedValue));
            Assert.Equal(value, storedValue);
        }

        [Fact]
        public void Keys()
        {
            const string foo = "foo", bar = "bar", baz = "baz";
            byte[] GenerateEmptyBytes() => new byte[0]; 
            _session.Set(foo, GenerateEmptyBytes());
            Assert.Equal(new[] { foo, }, _session.Keys);
            _session.Set(bar, GenerateEmptyBytes());
            Assert.Equal(new[] { foo, bar, }, _session.Keys);
            _session.Set(baz, GenerateEmptyBytes());
            Assert.Equal(new[] { foo, bar, baz, }, _session.Keys);
        }

        [Fact]
        public abstract Task CommitAsync();

        [Fact]
        public abstract Task LoadAsync();

        public abstract void Id(string id);

        public abstract void IsAvailable(bool isAvailable);
    }
}
