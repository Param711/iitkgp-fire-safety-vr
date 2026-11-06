using System;
using System.Reflection;
using FireSafety.Tests;

public static class RulesCheck
{
    public static int Main()
    {
        int passed=0,failed=0;var suite=new AssessmentRulesTests();
        foreach(var m in typeof(AssessmentRulesTests).GetMethods(BindingFlags.Public|BindingFlags.Instance))
        {
            if(m.GetCustomAttributes(typeof(NUnit.Framework.TestAttribute),true).Length==0)continue;
            try{m.Invoke(suite,null);Console.WriteLine("PASS: "+m.Name);passed++;}
            catch(Exception ex){Console.WriteLine("FAIL: "+m.Name+" / "+(ex.InnerException??ex));failed++;}
        }
        Console.WriteLine("Managed rules checks: "+passed+" passed, "+failed+" failed.");return failed==0?0:1;
    }
}
