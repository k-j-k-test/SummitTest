using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Flee.PublicTypes;

namespace SummitActuary
{
    public class PVPricing
    {
        public PVInfo[] PVInfos { get; set; }
        public PVCompiler PVCompiler { get; set; }
        public VariableCollection Variables { get; set; }

        public ILookup<string, string[]> MPLookup { get; set; }
        public ILookup<string, string[]> MPSTDLookup { get; set; }
        public ILookup<string, RiderTable> RiderLookup { get; set; }
        public ILookup<string, RiskRateTable> RiskRateLookup { get; set; }
        public ILookup<string, ExpenseTable> ExpneseLookup { get; set; }

        public string InputDirectory = @"C:\Users\wjdrh\OneDrive\Desktop\Test2\PV2테스트";
        public string OutputDirectory = @"";

        public PVPricing() 
        {
            PVCompiler = new PVCompiler();
            Variables = PVCompiler.Context.Variables;
        }

        public void PV산출()
        {
            PVInfo info = new PVInfo();

            변수초기화(info);






        }

        public void S산출()
        {

        }


        public void 변수초기화(PVInfo info)
        {
            Variables["x"] = info.x;
            Variables["n"] = info.n;
            Variables["m"] = info.m;
            Variables["Period"] = info.Period;
            Variables["SA"] = info.SA;

            Variables["F1"] = info.F1;
            Variables["F2"] = info.F2;
            Variables["F3"] = info.F3;
            Variables["F4"] = info.F4;
            Variables["F5"] = info.F5;
            Variables["F6"] = info.F6;
            Variables["F7"] = info.F7;
            Variables["F8"] = info.F8;
            Variables["F9"] = info.F9;

            Variables["S1"] = info.S1;
            Variables["S2"] = info.S2;
            Variables["S3"] = info.S3;
            Variables["S4"] = info.S4;
            Variables["S5"] = info.S5;
            Variables["S6"] = info.S6;
            Variables["S7"] = info.S7;
            Variables["S8"] = info.S8;
            Variables["S9"] = info.S9;

            //t dependent variables 
            Variables["i"] = 0.0;
            Variables["v"] = 0.0;
            Variables["w"] = 0.0;
            Variables["ii"] = 0.0;
            Variables["vv"] = 0.0;

            for (int i = 1; i <= 30; i++)
            {
                Variables["q" + i] = 0.0;
            }

            for (int i = 1; i <= 10; i++)
            {
                Variables["k" + i] = 0.0;
                Variables["r" + i] = 0.0;
            }
        }

        public void 모델포인트불러오기()
        {
            string path = Path.Combine(InputDirectory, "mp.txt");

            LTFStream stream = new LTFStream(path);
            MPLookup = stream.ReadAll().Select(line => line.Split('\t')).ToLookup(k => k[0] + "|"  + k[1]);
        }

        public void 담보정보불러오기()
        {
            string path = Path.Combine(InputDirectory, "rider.txt");

            LTFStream stream = new LTFStream(path);
            MPLookup = stream.ReadAll().Select(line => line.Split('\t')).ToLookup(k => k[0] + "|" + k[1]);
        }

        public void 위험률불러오기()
        {

        }

        public void 사업비불러오기()
        {

        }

        public void S값불러오기()
        {

        }


        public virtual void 모델포인트입력(PVInfo pvInfo)
        {

        }

        public virtual void 위험률입력(PVInfo pvInfo)
        {

        }

        public virtual void 사업비입력(PVInfo pvInfo)
        {

        }

        public virtual void 기수표생성(PVInfo pvInfo)
        {

        }

        public virtual void 순보험료계산(PVInfo pvInfo)
        {

        }

        public virtual void 영업보험료계산(PVInfo pvInfo)
        {

        }

        public virtual void 해약환급금계산(PVInfo pvInfo)
        {

        }

        public virtual void 결과출력(PVInfo pvInfo)
        {

        }

        
        public void ToRiderTable(string line)
        {
            RiderTable r = new RiderTable();
            string[] arr = line.Split('\t');

            r.ProductCode = arr[0];
            r.RiderCode = arr[1];
            r.RiderName = arr[2];
            r.PVType = ToInt(arr[3]);
            r.Stype = ToInt(arr[4]);
            r.MinSGroupKey = arr[5];
            r.Inforce = PVCompiler.CompileDouble(arr[6]);
            r.Payment = PVCompiler.CompileDouble(arr[7]);

            for (int i = 0; i < 10; i++)
            {
                if (!string.IsNullOrWhiteSpace(arr[8 + i]))
                {
                    r.Benefits[i + i] = PVCompiler.CompileDouble(arr[8 + i]);
                    r.Inforces[i + i] = string.IsNullOrWhiteSpace(arr[18 + i]) ? r.Inforce : PVCompiler.CompileDouble(arr[18 + i]);
                }
            }

            r.Benefit_Payment = PVCompiler.CompileDouble(arr[28]);
            r.Benefit_Waiver = PVCompiler.CompileDouble(arr[29]);

            for (int i = 0; i < 4; i++)
            {
                if (!string.IsNullOrWhiteSpace(arr[30 + i]))
                {
                    r.Benefits_State[i + i] = PVCompiler.CompileDouble(arr[30 + i]);
                    r.Inforces_State[i + i] = PVCompiler.CompileDouble(arr[34 + i]);
                }
            }

            for (int i = 0; i < 30; i++)
            {
                if (!string.IsNullOrWhiteSpace(arr[38 + i]))
                {
                    r.RiskRateNameMap[i + 1] = arr[38 + i];
                }
            }

            r.LapseRate = PVCompiler.CompileDouble(arr[68]);

            for (int i = 0; i < 10; i++)
            {
                if (!string.IsNullOrWhiteSpace(arr[69 + i]))
                {
                    r.Parameters_r[i + 1] = PVCompiler.CompileDouble(arr[69 + i]);
                    r.Parameters_k[i + 1] = PVCompiler.CompileDouble(arr[78 + i]);
                }
            }

        }

        public void ToRiskRateTable(string line)
        {
            RiskRateTable r = new RiskRateTable();
            string[] arr = line.Split('\t');

            r.RiskRateName = arr[0];
            r.F1 = ToInt(arr[1]);
            r.F2 = ToInt(arr[2]);
            r.F3 = ToInt(arr[3]);
            r.F4 = ToInt(arr[4]);
            r.F5 = ToInt(arr[5]);
            r.F6 = ToInt(arr[6]);
            r.F7 = ToInt(arr[7]);
            r.F8 = ToInt(arr[8]);
            r.F9 = ToInt(arr[9]);

            r.Condition = PVCompiler.CompileBool(arr[10]);
            r.RiskRates = Enumerable.Range(0, 131).Select(k => ToInt(arr[k + 11])).ToArray();
        }

        public void ToExpenseTable(string line)
        {
            ExpenseTable r = new ExpenseTable();
            string[] arr = line.Split('\t');

            r.ProductCode = arr[0];
            r.RiderCode = arr[1];
            r.Condition1 = PVCompiler.CompileBool(arr[2]);
            r.Condition2 = PVCompiler.CompileBool(arr[3]);
            r.Condition3 = PVCompiler.CompileBool(arr[4]);
            r.Condition4 = PVCompiler.CompileBool(arr[5]);

            r.Alpha_S = PVCompiler.CompileDouble(arr[6]);
            r.Alpha_P2 = PVCompiler.CompileDouble(arr[7]);

        }


        public double ToDouble(string s, double defaultVal = 0)
        {
            return double.TryParse(s, out double val) ? val : defaultVal;
        }

        public int ToInt(string s, int defaultVal = 0)
        {
            return int.TryParse(s, out int val) ? val : defaultVal;
        }
    }

    public class PVInfo
    {
        public string PropertyKey;

        public const int END_AGE = 131; // 0~130

        //Insured Property
        public int x { get; set; }
        public int n { get; set; }
        public int m { get; set; }
        public int Period { get; set; }
        public double SA { get; set; }

        public int F1 { get; set; }
        public int F2 { get; set; }
        public int F3 { get; set; }
        public int F4 { get; set; }
        public int F5 { get; set; }
        public int F6 { get; set; }
        public int F7 { get; set; }
        public int F8 { get; set; }
        public int F9 { get; set; }

        //Product or Rider Property
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public string RiderCode { get; set; }
        public string RiderName { get; set; }
        public int Jong { get; set; }

        public int S1 { get; set; }
        public int S2 { get; set; }
        public int S3 { get; set; }
        public int S4 { get; set; }
        public int S5 { get; set; }
        public int S6 { get; set; }
        public int S7 { get; set; }
        public int S8 { get; set; }
        public int S9 { get; set; }

        //Interest Rate
        public double[] i = new double[END_AGE];
        public double[] v = new double[END_AGE];
        public double[] w = new double[END_AGE];
        public double[] ii = new double[END_AGE];
        public double[] vv = new double[END_AGE];

        //RiskRate
        public Dictionary<string, double[]> RiskRates = new Dictionary<string, double[]>();

        //Expense
        public double Alpha_S { get; set; }
        public double Alpha_P { get; set; }
        public double Alpha_P2 { get; set; }
        public double Alpha_P20 { get; set; }
        public double Beta_S { get; set; }
        public double Beta_P { get; set; }
        public double Betaprime_S { get; set; }
        public double Betaprime_P { get; set; }
        public double Gamma { get; set; }
        public double Ce { get; set; }
        public double Refund_P { get; set; }
        public double Refund_S { get; set; }
        public double Exp_etc1 { get; set; }
        public double Exp_etc2 { get; set; }
        public double Exp_etc3 { get; set; }
        public double Exp_etc4 { get; set; }

        //Parameterized-Rate
        public double[] Rate_k1 = new double[END_AGE];
        public double[] Rate_k2 = new double[END_AGE];
        public double[] Rate_k3 = new double[END_AGE];
        public double[] Rate_k4 = new double[END_AGE];
        public double[] Rate_k5 = new double[END_AGE];
        public double[] Rate_k6 = new double[END_AGE];
        public double[] Rate_k7 = new double[END_AGE];
        public double[] Rate_k8 = new double[END_AGE];
        public double[] Rate_k9 = new double[END_AGE];

        public double[] Rate_r1 = new double[END_AGE];
        public double[] Rate_r2 = new double[END_AGE];
        public double[] Rate_r3 = new double[END_AGE];
        public double[] Rate_r4 = new double[END_AGE];
        public double[] Rate_r5 = new double[END_AGE];
        public double[] Rate_r6 = new double[END_AGE];
        public double[] Rate_r7 = new double[END_AGE];
        public double[] Rate_r8 = new double[END_AGE];
        public double[] Rate_r9 = new double[END_AGE];

        //Benefit
        public double[] Benefit_Inforce = new double[END_AGE];
        public double[] Benefit_Payment = new double[END_AGE];
        public double[] Benefit_Waiver = new double[END_AGE];
        public List<double[]> Benefit_Inforces = new List<double[]>();
        public List<double[]> Benefit_OtherStates = new List<double[]>();

        //Lx
        public double[] Lx_Inforce = new double[END_AGE];
        public double[] Lx_Payment = new double[END_AGE];
        public double[] Lx_Waiver = new double[END_AGE];
        public List<double[]> Lx_Inforces = new List<double[]>();
        public List<double[]> Lx_OtherStates = new List<double[]>();

        //Dx
        public double[] Dx_Inforce = new double[END_AGE];
        public double[] Dx_Payment = new double[END_AGE];
        public double[] Dx_Waiver = new double[END_AGE];
        public List<double[]> Dx_Inforces = new List<double[]>();
        public List<double[]> Dx_OtherStates = new List<double[]>();

        //Nx
        public double[] Nx_Inforce = new double[END_AGE];
        public double[] Nx_Payment = new double[END_AGE];
        public double[] Nx_Waiver = new double[END_AGE];
        public List<double[]> Nx_Inforces = new List<double[]>();
        public List<double[]> Nx_OtherStates = new List<double[]>();

        //Cx
        public double[] Cx_Inforce = new double[END_AGE];
        public double[] Cx_Payment = new double[END_AGE];
        public double[] Cx_Waiver = new double[END_AGE];
        public List<double[]> Cx_Inforces = new List<double[]>();
        public List<double[]> Cx_OtherStates = new List<double[]>();

        //Mx
        public double[] Mx_Inforce = new double[END_AGE];
        public double[] Mx_Payment = new double[END_AGE];
        public double[] Mx_Waiver = new double[END_AGE];
        public List<double[]> Mx_Inforces = new List<double[]>();
        public List<double[]> Mx_OtherStates = new List<double[]>();

        public double Mx_Inforces_Sum;
        public double Mx_Sum;

        //Premium, Reserve..
        public double NP;
        public double NP_Single;
        public double NP_Year;
        public double NP_Month;
        public double NP_2Months;
        public double NP_3Months;
        public double NP_6Months;
        public double NP_STD;
        public double NP_Patial_A;
        public double NP_Patial_B;
        public double NP_Patial_C;
        public double NP_Patial_D;

        public double GP;
        public double GP_Single;
        public double GP_Year;
        public double GP_Month;
        public double GP_2Month;
        public double GP_3Month;
        public double GP_6Month;
        public double GP_Patial_A;
        public double GP_Patial_B;
        public double GP_Patial_C;
        public double GP_Patial_D;

        public double ALPHA;
        public double ALPHA_Single;
        public double ALPHA_Year;
        public double ALPHA_Month;
        public double ALPHA_2Months;
        public double ALPHA_3Months;
        public double ALPHA_6Months;
        public double STDALPHA;

        public double[] V = new double[END_AGE];
        public double[] W = new double[END_AGE];
        public double[] DAC = new double[END_AGE];
        public double[] V_Patial_A = new double[END_AGE];
        public double[] V_Patial_B = new double[END_AGE];
        public double[] V_Patial_C = new double[END_AGE];
        public double[] V_Patial_D = new double[END_AGE];

        public double S;
        public double MinS;
    }

    public class PVCompiler
    {
        public ExpressionContext Context { get; private set; }

        private Dictionary<(Type, string), object> exprSet = new Dictionary<(Type, string), object>();

        public IGenericExpression<int> CompileInt(string exprStr)
        {
            if (string.IsNullOrWhiteSpace(exprStr)) exprStr = "0";

            if (exprSet.ContainsKey((typeof(int), exprStr)))
            {
                return (IGenericExpression<int>)exprSet[(typeof(int), exprStr)];
            }
            else
            {
                try
                {
                    exprSet[(typeof(int), exprStr)] = Context.CompileGeneric<int>(exprStr);
                    return (IGenericExpression<int>)exprSet[(typeof(int), exprStr)];
                }
                catch
                {
                    throw new Exception("적용 할 수 없는 수식 발견:" + exprStr);
                }
            }
        }

        public IGenericExpression<bool> CompileBool(string exprStr)
        {
            if (string.IsNullOrWhiteSpace(exprStr)) exprStr = "true";

            if (exprSet.ContainsKey((typeof(bool), exprStr)))
            {
                return (IGenericExpression<bool>)exprSet[(typeof(bool), exprStr)];
            }
            else
            {
                try
                {
                    exprSet[(typeof(bool), exprStr)] = Context.CompileGeneric<bool>(exprStr);
                    return (IGenericExpression<bool>)exprSet[(typeof(bool), exprStr)];
                }
                catch
                {
                    throw new Exception("적용 할 수 없는 수식 발견:" + exprStr);
                }
            }
        }

        public IGenericExpression<double> CompileDouble(string exprStr)
        {
            if (string.IsNullOrWhiteSpace(exprStr)) exprStr = "0";
            if (exprStr == "∞" || exprStr == "NaN") exprStr = "0";

            if (exprSet.ContainsKey((typeof(double), exprStr)))
            {
                return (IGenericExpression<double>)exprSet[(typeof(double), exprStr)];
            }
            else
            {
                try
                {
                    exprSet[(typeof(double), exprStr)] = Context.CompileGeneric<double>(exprStr);
                    return (IGenericExpression<double>)exprSet[(typeof(double), exprStr)];
                }
                catch
                {
                    throw new Exception("적용 할 수 없는 수식 발견:" + exprStr);
                }
            }
        }

        public IGenericExpression<string> CompileString(string exprStr)
        {
            if (string.IsNullOrWhiteSpace(exprStr)) exprStr = @""""""; //""

            if (exprSet.ContainsKey((typeof(string), exprStr)))
            {
                return (IGenericExpression<string>)exprSet[(typeof(string), exprStr)];
            }
            else
            {
                try
                {
                    exprSet[(typeof(string), exprStr)] = Context.CompileGeneric<string>(exprStr);
                    return (IGenericExpression<string>)exprSet[(typeof(string), exprStr)];
                }
                catch
                {
                    throw new Exception("적용 할 수 없는 수식 발견:" + exprStr);
                }
            }
        }     
    }

    //Tables
    public class RiderTable
    {
        public string ProductCode { get; set; }
        public string RiderCode { get; set; }
        public string RiderName { get; set; }
        public int PVType { get; set; }
        public int Stype { get; set; }
        public string MinSGroupKey { get; set; }

        public IGenericExpression<double> Inforce { get; set; }
        public IGenericExpression<double> Payment { get; set; }
        public Dictionary<int, IGenericExpression<double>> Benefits { get; set; }
        public Dictionary<int, IGenericExpression<double>> Inforces { get; set; }
        public IGenericExpression<double> Benefit_Payment { get; set; }
        public IGenericExpression<double> Benefit_Waiver { get; set; }
        public Dictionary<int, IGenericExpression<double>> Benefits_State { get; set; }
        public Dictionary<int, IGenericExpression<double>> Inforces_State { get; set; }

        public Dictionary<int, string> RiskRateNameMap { get; set; }
        public IGenericExpression<double> LapseRate { get; set; }

        public Dictionary<int, IGenericExpression<double>> Parameters_r { get; set; }
        public Dictionary<int, IGenericExpression<double>> Parameters_k { get; set; }
    }

    public class MPTable
    {
        public string ProductCode { get; set; }
        public string RiderCode { get; set; }
        public int Jong { get; set; }
        public int x { get; set; }
        public int n { get; set; }
        public int m { get; set; }
        public int Period { get; set; }
        public double SA { get; set; }
        public int F1 { get; set; }
        public int F2 { get; set; }
        public int F3 { get; set; }
        public int F4 { get; set; }
        public int F5 { get; set; }
        public int F6 { get; set; }
        public int F7 { get; set; }
        public int F8 { get; set; }
        public int F9 { get; set; }
        public int S1 { get; set; }
        public int S2 { get; set; }
        public int S3 { get; set; }
        public int S4 { get; set; }
        public int S5 { get; set; }
        public int S6 { get; set; }
        public int S7 { get; set; }
        public int S8 { get; set; }
        public int S9 { get; set; }
    }

    public class RiskRateTable
    {
        public string RiskRateName { get; set; }
        public int F1 { get; set; }
        public int F2 { get; set; }
        public int F3 { get; set; }
        public int F4 { get; set; }
        public int F5 { get; set; }
        public int F6 { get; set; }
        public int F7 { get; set; }
        public int F8 { get; set; }
        public int F9 { get; set; }
        public IGenericExpression<bool> Condition { get; set; }
        public int[] RiskRates { get; set; }
    }

    public class ExpenseTable
    {
        public string ProductCode { get; set; }
        public string RiderCode { get; set; }
        public IGenericExpression<bool> Condition1 { get; set; }
        public IGenericExpression<bool> Condition2 { get; set; }
        public IGenericExpression<bool> Condition3 { get; set; }
        public IGenericExpression<bool> Condition4 { get; set; }
        public IGenericExpression<double> Alpha_S { get; set; }
        public IGenericExpression<double> Alpha_P { get; set; }
        public IGenericExpression<double> Alpha_P2 { get; set; }
        public IGenericExpression<double> Alpha_P20 { get; set; }
        public IGenericExpression<double> Beta_S { get; set; }
        public IGenericExpression<double> Beta_P { get; set; }
        public IGenericExpression<double> Betaprime_S { get; set; }
        public IGenericExpression<double> Betaprime_P { get; set; }
        public IGenericExpression<double> Gamma { get; set; }
        public IGenericExpression<double> Ce { get; set; }
        public IGenericExpression<double> Refund_P { get; set; }
        public IGenericExpression<double> Refund_S { get; set; }
        public IGenericExpression<double> Exp_etc1 { get; set; }
        public IGenericExpression<double> Exp_etc2 { get; set; }
        public IGenericExpression<double> Exp_etc3 { get; set; }
        public IGenericExpression<double> Exp_etc4 { get; set; }
    }
}
