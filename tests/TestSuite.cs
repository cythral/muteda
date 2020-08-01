using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using NSubstitute;

using NUnit.Framework;

namespace Mutedac
{
    public abstract class TestSuite<TContext> where TContext : TestSuite<TContext>.Context, new()
    {
        public abstract class Context
        {
            public virtual Task Setup()
            {
                return Task.Run(() => { });
            }
        }

        public async Task<TContext> GetContext()
        {
            var context = new TContext();
            var fields = typeof(TContext)
                .GetFields()
                .Where(field => field.GetCustomAttributes(typeof(SubstituteAttribute), true).Count() > 0);

            foreach (var field in fields)
            {
                var bindingFlags = BindingFlags.Static | BindingFlags.Public;
                var subForMethod = typeof(Substitute).GetMethod("For", 1, bindingFlags, null, new Type[] { typeof(object[]) }, null);
                var subForGeneric = subForMethod?.MakeGenericMethod(new Type[] { field.FieldType });
                var sub = subForGeneric?.Invoke(null, new object[] { new object[] { } });

                field.SetValue(context, sub);
            }

            await context.Setup();
            return context;
        }
    }
}