using Flee.PublicTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SummitActuary
{
    public class PVCompiler
    {
        public ExpressionContext Context { get; private set; }

        private Dictionary<(Type, string), object> exprSet = new Dictionary<(Type, string), object>();

        public PVCompiler(PVPricing owner)
        {
            Context = new ExpressionContext(owner);
            Context.Options.OwnerMemberAccess = System.Reflection.BindingFlags.Public |
                                                System.Reflection.BindingFlags.NonPublic;
        }

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

    public partial class PVPricing
    {
        public double D(params double[] items)
        {
            int t = (int)Variables["t"];
            int S1 = (int)Variables["S1"];

            if (S1 > 0 || t >= items.Length)
            {
                return 1.0;
            }
            else
            {
                return items[t];
            }
        }

        public double U(params double[] items)
        {
            int t = (int)Variables["t"];
            int x = (int)Variables["x"];
            int S1 = (int)Variables["S1"];

            if (S1 > 0 || x + t < 15 || t >= items.Length)
            {
                return 1.0;
            }
            else
            {
                return items[t];
            }
        }

        public double Round(double value, int decimals)
        {
            double preRounded = Math.Round(value, 12);
            double factor = Math.Pow(10, decimals);
            return Math.Round(preRounded * factor) / factor;
        }

        public double RoundDown(double value, int decimals)
        {
            double preRounded = Math.Round(value, 12);
            double factor = Math.Pow(10, decimals);
            return Math.Floor(preRounded * factor) / factor;
        }

        public double RoundUp(double value, int decimals)
        {
            double preRounded = Math.Round(value, 12);
            double factor = Math.Pow(10, decimals);
            return Math.Ceiling(preRounded * factor) / factor;
        }

        public double RoundSA(double number)
        {
            double SA = (double)Variables["SA"];
            return Round(number * SA, 0) / SA;
        }

        public double Min(params double[] values)
        {
            return values.Min();
        }

        public double Max(params double[] values)
        {
            return values.Max();
        }

        public int Min(params int[] values)
        {
            return values.Min();
        }

        public int Max(params int[] values)
        {
            return values.Max();
        }

        public double FindQ(string rateName, int t)
        {
            if (!RiskRateLookup.Contains(rateName))
            {
                throw new Exception("위험률이 존재하지 않습니다. " + rateName);
            }

            PVInfo info = PVInfo;

            foreach (RiskRateTable riskrate in RiskRateLookup[rateName])
            {
                if (riskrate.F1 != null && info.MP.F1 != riskrate.F1.Value) continue;
                if (riskrate.F2 != null && info.MP.F2 != riskrate.F2.Value) continue;
                if (riskrate.F3 != null && info.MP.F3 != riskrate.F3.Value) continue;
                if (riskrate.F4 != null && info.MP.F4 != riskrate.F4.Value) continue;
                if (riskrate.F5 != null && info.MP.F5 != riskrate.F5.Value) continue;
                if (riskrate.F6 != null && info.MP.F6 != riskrate.F6.Value) continue;
                if (riskrate.F7 != null && info.MP.F7 != riskrate.F7.Value) continue;
                if (riskrate.F8 != null && info.MP.F8 != riskrate.F8.Value) continue;
                if (riskrate.F9 != null && info.MP.F9 != riskrate.F9.Value) continue;

                return riskrate.RiskRates[t] / riskrate.Face;
            }

            throw new Exception("조건에 맞는 위험률을 찾을 수 없습니다. " + rateName);
        }

        public double V(int t)
        {
            int S2 = (int)Variables["S2"];
            int S3 = (int)Variables["S3"];
            int S5 = (int)Variables["S5"];

            if (S5 == 0) return 0;
            string key = string.Join("|", S2, S3, 0);
            return RoundSA(PVInfos[key].V[t]);
        }

        public double W(int t)
        {
            int S2 = (int)Variables["S2"];
            int S3 = (int)Variables["S3"];
            int S5 = (int)Variables["S5"];

            if (S5 == 0) return 0;
            string key = string.Join("|", S2, S3, 0);
            return RoundSA(PVInfos[key].W[t]);
        }
    }
}
