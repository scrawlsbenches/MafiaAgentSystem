using System.Reflection;
using TestRunner.Framework;

var runner = new SimpleTestRunner();
runner.DiscoverTests(Assembly.GetExecutingAssembly());
return await runner.RunAllAsync();
