using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace NineChronicles.Headless.Tests
{
    public class InMemorySessionTest : SessionTest
    {
        public InMemorySessionTest()
            : base(new InMemorySession(string.Empty, true))
        {
        }

        [Theory]
        [InlineData("foo")]
        [InlineData("bar")]
        [InlineData("baz")]
        public override void Id(string id)
        {
            var session = new InMemorySession(id, true);
            Assert.Equal(id, session.Id);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public override void IsAvailable(bool isAvailable)
        {
            var session = new InMemorySession(string.Empty, isAvailable);
            Assert.Equal(isAvailable, session.IsAvailable);
        }

        [Fact]
        public override Task CommitAsync()
        {   
            var session = new InMemorySession(string.Empty, true);
            Assert.Equal(Task.CompletedTask, session.CommitAsync());
            return Task.CompletedTask;
        }

        [Fact]
        public override Task LoadAsync()
        {
            var session = new InMemorySession(string.Empty, true);
            Assert.Equal(Task.CompletedTask, session.LoadAsync());
            return Task.CompletedTask;
        }
    }
}
