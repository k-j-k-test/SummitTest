using Flee.PublicTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SummitActuary
{
    //Tables
    public class ProductTable
    {
        public string ProductCode { get; set; }
        public int Jong { get; set; }
        public string ProductName { get; set; }
        public string Date { get; set; }
        public IGenericExpression<double> i { get; set; }
        public IGenericExpression<double> ii { get; set; }
        public IGenericExpression<double> w { get; set; }
        public int Channel { get; set; }
    }

    public class RiderTable
    {
        public string ProductCode { get; set; }
        public string RiderCode { get; set; }
        public int Jong { get; set; }
        public string RiderName { get; set; }
        public IGenericExpression<int> PVType { get; set; }
        public IGenericExpression<int> Stype { get; set; }

        public IGenericExpression<double> Inforce { get; set; }
        public IGenericExpression<double> Payment { get; set; }
        public Dictionary<int, IGenericExpression<double>> Benefits { get; set; } = new Dictionary<int, IGenericExpression<double>>();
        public Dictionary<int, IGenericExpression<double>> Inforces { get; set; } = new Dictionary<int, IGenericExpression<double>>();
        public IGenericExpression<double> Benefit_Inforce { get; set; }
        public IGenericExpression<double> Benefit_Payment { get; set; }
        public IGenericExpression<double> Benefit_Waiver { get; set; }
        public Dictionary<int, IGenericExpression<double>> Benefits_State { get; set; } = new Dictionary<int, IGenericExpression<double>>();
        public Dictionary<int, IGenericExpression<double>> Inforces_State { get; set; } = new Dictionary<int, IGenericExpression<double>>();

        public Dictionary<int, string> RiskRateNameMap { get; set; } = new Dictionary<int, string>();

        public Dictionary<int, IGenericExpression<double>> Parameters_r { get; set; } = new Dictionary<int, IGenericExpression<double>>();
        public Dictionary<int, IGenericExpression<double>> Parameters_k { get; set; } = new Dictionary<int, IGenericExpression<double>>();
    }

    public class ModelPointTable
    {
        public string ProductCode { get; set; }
        public string RiderCode { get; set; }
        public int Jong { get; set; }
        public int x { get; set; }
        public int n { get; set; }
        public int m { get; set; }
        public int Freq { get; set; }
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

        public string GetKey()
        {
            return string.Join("|", S2, S3, S5);
        }

        public ModelPointTable Clone()
        {
            return (ModelPointTable)this.MemberwiseClone();
        }

        public override string ToString()
        {
            return string.Join("\t", ProductCode, RiderCode, Jong, x, n, m, Freq, SA, F1, F2, F3, F4, F5, F6, F7, F8, F9, S1, S2, S3, S4, S5, S6, S7, S8, S9);
        }
    }

    public class StandardAgeTable
    {
        public string ProductCode { get; set; }
        public string RiderCode { get; set; }
        public int Jong { get; set; }
        public int x { get; set; }
        public int n { get; set; }
        public int m { get; set; }
        public int Freq { get; set; }
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

        public IGenericExpression<bool> MinSGroup_Condition1 { get; set; }
        public IGenericExpression<bool> MinSGroup_Condition2 { get; set; }
        public IGenericExpression<bool> MinSGroup_Condition3 { get; set; }
        public double NP { get; set; }
        public double TermNP { get; set; }
        public double S { get; set; }

        public ModelPointTable ToModelPoint()
        {
            ModelPointTable mp = new ModelPointTable();

            mp.ProductCode = this.ProductCode;
            mp.RiderCode = this.RiderCode;
            mp.Jong = this.Jong;
            mp.x = this.x;
            mp.n = this.n;
            mp.m = this.m;
            mp.Freq = this.Freq;
            mp.SA = this.SA;
            mp.F1 = this.F1;
            mp.F2 = this.F2;
            mp.F3 = this.F3;
            mp.F4 = this.F4;
            mp.F5 = this.F5;
            mp.F6 = this.F6;
            mp.F7 = this.F7;
            mp.F8 = this.F8;
            mp.F9 = this.F9;
            mp.S1 = this.S1;
            mp.S2 = this.S2;
            mp.S3 = this.S3;
            mp.S4 = this.S4;
            mp.S5 = this.S5;
            mp.S6 = this.S6;
            mp.S7 = this.S7;
            mp.S8 = this.S8;
            mp.S9 = this.S9;

            return mp;
        }

        public override string ToString()
        {
            return string.Join("\t",
                ProductCode, RiderCode, Jong, x, n, m, Freq, SA,
                F1, F2, F3, F4, F5, F6, F7, F8, F9,
                S1, S2, S3, S4, S5, S6, S7, S8, S9,
                MinSGroup_Condition1.Text, MinSGroup_Condition2.Text, MinSGroup_Condition3.Text,
                NP, TermNP, S);
        }
    }

    public class RiskRateTable
    {
        public string RiskRateName { get; set; }
        public Nullable<int> F1 { get; set; }
        public Nullable<int> F2 { get; set; }
        public Nullable<int> F3 { get; set; }
        public Nullable<int> F4 { get; set; }
        public Nullable<int> F5 { get; set; }
        public Nullable<int> F6 { get; set; }
        public Nullable<int> F7 { get; set; }
        public Nullable<int> F8 { get; set; }
        public Nullable<int> F9 { get; set; }

        public string Date { get; set; }
        public double Face { get; set; }
        public IGenericExpression<int> Offset { get; set; }
        public double[] RiskRates { get; set; }
    }

    public class ExpenseTable
    {
        public string ProductCode { get; set; }
        public string RiderCode { get; set; }
        public int Jong { get; set; }
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
