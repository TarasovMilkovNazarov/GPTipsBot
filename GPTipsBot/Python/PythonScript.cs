using Microsoft.Scripting.Hosting;
using Microsoft.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Python
{
    public class PythonScript
    {
        private ScriptEngine _engine;

        public PythonScript()
        {
            _engine = IronPython.Hosting.Python.CreateEngine();
        }

        public TResult RunFromString<TResult>(string variableName)
        {
            // for easier debugging write it out to a file and call: _engine.CreateScriptSourceFromFile(filePath);
            ScriptSource source = _engine.CreateScriptSourceFromFile("bertscore_script.py");
            CompiledCode cc = source.Compile();

            ScriptScope scope = _engine.CreateScope();
            cc.Execute(scope);

            return scope.GetVariable<TResult>(variableName);
        }
    }
}
