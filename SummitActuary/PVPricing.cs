using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Flee.PublicTypes;

namespace SummitActuary
{
    public partial class PVPricing
    {
        public PVInfo PVInfo { get; set; }
        public Dictionary<string, PVInfo> PVInfos { get; set; }

        public PVCompiler PVCompiler { get; set; }
        public VariableCollection Variables { get; set; }
        public DataExpander<ModelPointTable> DataExpander { get; set; }

        public List<string[]> MPs { get; set; }
        public ILookup<string, StandardAgeTable> StandardAgeLookup { get; set; }
        public ILookup<string, ProductTable> ProductLookup { get; set; }
        public ILookup<string, RiderTable> RiderLookup { get; set; }
        public ILookup<string, RiskRateTable> RiskRateLookup { get; set; }
        public ILookup<string, ExpenseTable> ExpneseLookup { get; set; }
 
        public string InputDirectory = @"C:\Users\wjdrh\OneDrive\Desktop\Example1\Data";
        public string OutputDirectory = @"C:\Users\wjdrh\OneDrive\Desktop\Example1\Result";
        public string OutputFileSuffix { get; set; } = "";

        public StreamWriter PremiumWriter { get; set; }
        public StreamWriter ReserveWriter { get; set; }
        public StreamWriter StandardAgeWriter { get; set; }

        public PVPricing()
        {
            PVCompiler = new PVCompiler(this);
            Variables = PVCompiler.Context.Variables;
            DataExpander = new DataExpander<ModelPointTable>();
            PVInfos = new Dictionary<string, PVInfo>();
            SetVariables(new ModelPointTable());
        }

        public void PV산출(string productCode, string riderCode = "", int jong = 0)
        {
            모델포인트불러오기();
            상품정보불러오기();
            담보정보불러오기();
            위험률불러오기();
            사업비불러오기();
            기준연령불러오기_계산후();

            List<string[]> targetMPLines = MPs
                .Where(arr => arr[0] == productCode)
                .Where(arr => arr[1] == riderCode || riderCode == "")
                .Where(arr => arr[2] == jong.ToString() || jong == 0)
                .ToList();

            int count = 0;
            int total = targetMPLines.Count;

            using (PremiumWriter = new StreamWriter(Path.Combine(OutputDirectory, $"Premium_Result_{OutputFileSuffix}.txt"), default))
            using (ReserveWriter = new StreamWriter(Path.Combine(OutputDirectory, $"Reserve_Result_{OutputFileSuffix}.txt"), default))
            {
                PremiumWriter.WriteLine("ProductCode\tRiderCode\tJong\tx\tn\tm\tFreq\tSA\tF1\tF2\tF3\tF4\tF5\tF6\tF7\tF8\tF9\tS1\tS2\tS3\tS4\tS5\tS6\tS7\tS8\tS9\tNP12\tGP12");
                ReserveWriter.WriteLine("ProductCode\tRiderCode\tJong\tx\tn\tm\tFreq\tSA\tF1\tF2\tF3\tF4\tF5\tF6\tF7\tF8\tF9\tS1\tS2\tS3\tS4\tS5\tS6\tS7\tS8\tS9\tV");

                foreach (var mpLine in targetMPLines)
                {
                    List<ModelPointTable> mps = DataExpander.ExpandData(mpLine);
                    foreach (var mp in mps)
                    {
                        PVInfo계산(mp);
                        PVInfo info = PVInfo;

                        PremiumWriter.WriteLine(string.Join("\t", info.MP.ToString(), SA(info.NP[12]), SA(info.GP[12])));
                        ReserveWriter.WriteLine(info.MP.ToString() + "\t" + string.Join("\t", info.V.Take(info.MP.n + 1)));
                    }

                    count++;
                    Console.Write($"\r진행률: {count}/{total} ({count * 100 / total}%)");
                }
            }
        }

        public void S산출(string productCode, string riderCode = "")
        {
            모델포인트불러오기();
            상품정보불러오기();
            담보정보불러오기();
            위험률불러오기();
            사업비불러오기();
            기준연령불러오기_계산전();

            List<StandardAgeTable> targetStandardAges = StandardAgeLookup
                .Where(k => k.Key.Split('|')[0] == productCode)
                .Where(k => riderCode == "" || k.Key.Split('|')[1] == riderCode)
                .SelectMany(k => k)
                .ToList();

            using (StandardAgeWriter = new StreamWriter(Path.Combine(InputDirectory, "StandardAge_Calculated.txt"), default))
            {
                foreach (StandardAgeTable line in targetStandardAges)
                {
                    ModelPointTable mp = line.ToModelPoint();                   

                    PVInfo계산(mp);
                    PVInfo info = PVInfo;

                    line.NP = info.NP[12];
                    line.TermNP = 정기사망위험보험료계산(info);
                    line.S = line.NP / line.TermNP;

                    StandardAgeWriter.WriteLine(line.ToString());
                }
            }
        }


        public void 모델포인트불러오기()
        {
            string path = Path.Combine(InputDirectory, "MP.txt");

            LTFStream stream = new LTFStream(path);
            MPs = stream.ReadAll().Select(line => line.Split('\t')).ToList();
        }

        public void 상품정보불러오기()
        {
            string path = Path.Combine(InputDirectory, "Product.txt");

            LTFStream stream = new LTFStream(path);
            ProductLookup = stream.ReadAll().Select(line => ToProductTable(line)).ToLookup(k => k.ProductCode + "|" + k.Jong);
        }

        public void 담보정보불러오기()
        {
            string path = Path.Combine(InputDirectory, "Rider.txt");

            LTFStream stream = new LTFStream(path);
            RiderLookup = stream.ReadAll().Select(line => ToRiderTable(line)).ToLookup(k => k.ProductCode + "|" + k.RiderCode + "|" + k.Jong);
        }

        public void 위험률불러오기()
        {
            string path = Path.Combine(InputDirectory, "RiskRate.txt");

            LTFStream stream = new LTFStream(path);
            RiskRateLookup = stream.ReadAll().Select(line => ToRiskRateTable(line)).ToLookup(k => k.RiskRateName);
        }

        public void 사업비불러오기()
        {
            string path = Path.Combine(InputDirectory, "Expense.txt");

            LTFStream stream = new LTFStream(path);
            ExpneseLookup = stream.ReadAll().Select(line => ToExpenseTable(line)).ToLookup(k => k.ProductCode + "|" + k.RiderCode + "|" + k.Jong);
        }

        public void 기준연령불러오기_계산전()
        {
            string path = Path.Combine(InputDirectory, "StandardAge.txt");

            if (File.Exists(path))
            {
                LTFStream stream = new LTFStream(path);
                StandardAgeLookup = stream.ReadAll().Select(line => ToStandardAgeTable(line)).ToLookup(k => k.ProductCode + "|" + k.RiderCode + "|" + k.Jong);
            }
        }

        public void 기준연령불러오기_계산후()
        {
            string path = Path.Combine(InputDirectory, "StandardAge_Calculated.txt");

            if (File.Exists(path))
            {
                LTFStream stream = new LTFStream(path);
                StandardAgeLookup = stream.ReadAll().Select(line => ToStandardAgeTable(line)).ToLookup(k => k.ProductCode + "|" + k.RiderCode + "|" + k.Jong);
            }
        }


        public void 상품정보입력(PVInfo info)
        {
            string key1 = info.MP.ProductCode + "|" + info.MP.Jong;
            string key2 = info.MP.ProductCode + "|" + 0;

            if (ProductLookup.Contains(key1))
            {
                info.Product = ProductLookup[key1].FirstOrDefault();
            }
            else if (ProductLookup.Contains(key2))
            {
                info.Product = ProductLookup[key2].FirstOrDefault();
            }
            else
            {
                throw new Exception("상품정보가 존재하지 않습니다. " + key1);
            }
        }

        public void 담보정보입력(PVInfo info)
        {
            string key1 = info.MP.ProductCode + "|" + info.MP.RiderCode + "|" + info.MP.Jong;
            string key2 = info.MP.ProductCode + "|" + info.MP.RiderCode + "|" + 0;

            if(RiderLookup.Contains(key1))
            {
                info.Rider = RiderLookup[key1].FirstOrDefault();
            }
            else if (RiderLookup.Contains(key2))
            {
                info.Rider = RiderLookup[key2].FirstOrDefault();
            }
            else
            {
                throw new Exception("담보정보가 존재하지 않습니다. " + key1);
            }
        }

        public void 위험률입력(PVInfo info)
        {
            info.RiskRates = new Dictionary<int, double[]>();
            foreach (var kpv in info.Rider.RiskRateNameMap)
            {
                int index = kpv.Key;
                string rateName = kpv.Value;

                double[] rates = FindRiskRate(info, rateName);
                info.RiskRates[index] = rates;
            }
        }

        public void 사업비입력(PVInfo info)
        {
            string key1 = info.MP.ProductCode + "|" + info.MP.RiderCode + "|" + info.MP.Jong;
            string key2 = info.MP.ProductCode + "|" + info.MP.RiderCode + "|" + 0;
            string key3 = info.MP.ProductCode + "|" + "" + "|" + info.MP.Jong;
            string key4 = info.MP.ProductCode + "|" + "" + "|" + 0;

            List<ExpenseTable> expenseList = new List<ExpenseTable>();

            if (ExpneseLookup.Contains(key1))
            {
                expenseList = ExpneseLookup[key1].ToList();
            }
            else if (ExpneseLookup.Contains(key2))
            {
                expenseList = ExpneseLookup[key2].ToList();
            }
            else if (ExpneseLookup.Contains(key3))
            {
                expenseList = ExpneseLookup[key3].ToList();
            }
            else if (ExpneseLookup.Contains(key4))
            {
                expenseList = ExpneseLookup[key4].ToList();
            }

            foreach (ExpenseTable expense in expenseList)
            {
                if (!expense.Condition1.Evaluate()) continue;
                if (!expense.Condition2.Evaluate()) continue;
                if (!expense.Condition3.Evaluate()) continue;
                if (!expense.Condition4.Evaluate()) continue;

                info.Alpha_S = expense.Alpha_S.Evaluate();
                info.Alpha_P = expense.Alpha_P.Evaluate();
                info.Alpha_P2 = expense.Alpha_P2.Evaluate();
                info.Alpha_P20 = expense.Alpha_P20.Evaluate();
                info.Beta_S = expense.Beta_S.Evaluate();
                info.Beta_P = expense.Beta_P.Evaluate();
                info.Betaprime_S = expense.Betaprime_S.Evaluate();
                info.Betaprime_P = expense.Betaprime_P.Evaluate();
                info.Gamma = expense.Gamma.Evaluate();
                info.Ce = expense.Ce.Evaluate();
                info.Refund_P = expense.Refund_P.Evaluate();
                info.Refund_S = expense.Refund_S.Evaluate();
                info.Exp_etc1 = expense.Exp_etc1.Evaluate();
                info.Exp_etc2 = expense.Exp_etc2.Evaluate();
                info.Exp_etc3 = expense.Exp_etc3.Evaluate();
                info.Exp_etc4 = expense.Exp_etc4.Evaluate();

                return;
            }

            throw new Exception("사업비가 존재하지 않습니다. " + key1);
        }

        public void MinS값입력(PVInfo info)
        {
            string key = info.MP.ProductCode + "|" + info.MP.RiderCode + "|" + info.MP.Jong;

            if (StandardAgeLookup != null && StandardAgeLookup.Contains(key))
            {
                List<StandardAgeTable> standardAges = StandardAgeLookup[key]
                    .Where(line => line.MinSGroup_Condition1.Evaluate() && line.MinSGroup_Condition2.Evaluate() && line.MinSGroup_Condition3.Evaluate())
                    .ToList();

                info.MinS = standardAges.Any() ? standardAges.Min(line => line.S) : 0;
            }
            else
            {
                info.MinS = 0;
            }
        }

        public void 기수표생성(PVInfo info)
        {
            int x = info.MP.x;
            int n = info.MP.n;
            int m = info.MP.m;

            foreach (var kpv in info.Rider.Parameters_r) info.Rate_r[kpv.Key] = new double[PVInfo.END_AGE];
            foreach (var kpv in info.Rider.Parameters_k) info.Rate_k[kpv.Key] = new double[PVInfo.END_AGE];
            foreach (var kpv in info.Rider.Benefits) info.Benefit_Inforces[kpv.Key] = new double[PVInfo.END_AGE];
            foreach (var kpv in info.Rider.Inforces) info.Survivor_Inforces[kpv.Key] = new double[PVInfo.END_AGE];
            foreach (var kpv in info.Rider.Benefits_State) info.Benefit_States[kpv.Key] = new double[PVInfo.END_AGE];
            foreach (var kpv in info.Rider.Inforces_State) info.Survivor_States[kpv.Key] = new double[PVInfo.END_AGE];

            for (int i = 0; i < n; i++)
            {
                SetVariables(info, i);

                info.i[i] = (double)Variables["i"];
                info.ii[i] = (double)Variables["ii"];
                info.v[i] = (double)Variables["v"];
                info.vv[i] = (double)Variables["vv"];
                info.w[i] = (double)Variables["w"];
                info.v_Acc[i] = (i == 0) ? 1.0 : info.v_Acc[i - 1] * info.v[i - 1];
                info.v_AccMid[i] = info.v_Acc[i] * Math.Pow(info.v[i], 0.5);

                //Survival, Benefit
                info.Survivor_Inforce[i] = info.Rider.Inforce.Evaluate();
                info.Survivor_Payment[i] = info.Rider.Payment.Evaluate();

                info.Benefit_Inforce[i] = info.Rider.Benefit_Inforce.Evaluate();
                info.Benefit_Payment[i] = info.Rider.Benefit_Payment.Evaluate();
                info.Benefit_Waiver[i] = info.Rider.Benefit_Waiver.Evaluate();

                foreach (var kpv in info.Rider.Benefits)
                {
                    info.Benefit_Inforces[kpv.Key][i] = kpv.Value.Evaluate();
                }

                foreach (var kpv in info.Rider.Inforces)
                {
                    info.Survivor_Inforces[kpv.Key][i] = kpv.Value.Evaluate();
                }

                foreach (var kpv in info.Rider.Benefits_State)
                {
                    info.Benefit_States[kpv.Key][i] = kpv.Value.Evaluate();
                }

                foreach (var kpv in info.Rider.Inforces_State)
                {
                    info.Survivor_States[kpv.Key][i] = kpv.Value.Evaluate();
                }
            }

            //Lx
            info.Lx_Inforce = Lx(info.Survivor_Inforce, n);
            info.Lx_Payment = Lx(info.Survivor_Payment, n);
            info.Lx_Waiver = Enumerable.Range(0, n + 1).Select(i => info.Survivor_Inforce[i] - info.Survivor_Payment[i]).ToArray();

            foreach (var kpv in info.Rider.Inforces)
            {
                info.Lx_Inforces[kpv.Key] = Lx(info.Survivor_Inforces[kpv.Key], n);
            }

            //Dx
            info.Dx_Inforce = Dx(info.Lx_Inforce, info.v_Acc, n);
            info.Dx_Payment = Dx(info.Lx_Payment, info.v_Acc, n);
            info.Dx_Waiver = Dx(info.Lx_Waiver, info.v_Acc, n);

            foreach (var kpv in info.Lx_Inforces)
            {
                info.Dx_Inforces[kpv.Key] = Dx(kpv.Value, info.v_Acc, n);
            }

            //Nx                                                                                                                                        
            info.Nx_Inforce = Nx(info.Dx_Inforce, n);
            info.Nx_Payment = Nx(info.Dx_Payment, n);
            info.Nx_Waiver = Nx(info.Dx_Waiver, n);

            foreach (var kpv in info.Dx_Inforces)
            {
                info.Nx_Inforces[kpv.Key] = Nx(kpv.Value, n);
            }

            //Cx
            info.Cx_Inforce = Cx(info.Lx_Inforce, info.v_AccMid, info.Benefit_Inforce, n);
            info.Cx_Payment = Cx(info.Lx_Payment, info.v_AccMid, info.Benefit_Payment, n);
            info.Cx_Waiver = Cx(info.Lx_Waiver, info.v_AccMid, info.Benefit_Waiver, n);

            foreach (var kpv in info.Benefit_Inforces)
            {
                if (info.Lx_Inforces.ContainsKey(kpv.Key))
                {
                    info.Cx_Inforces[kpv.Key] = Cx(info.Lx_Inforces[kpv.Key], info.v_AccMid, kpv.Value, n);
                }
            }

            //Mx
            info.Mx_Inforce = Mx(info.Cx_Inforce, n);
            info.Mx_Payment = Mx(info.Cx_Payment, n);
            info.Mx_Waiver = Mx(info.Cx_Waiver, n);

            foreach (var kpv in info.Cx_Inforces)
            {
                info.Mx_Inforces[kpv.Key] = Mx(kpv.Value, n);
            }

            for (int i = 0; i <= n; i++)
            {
                info.Mx_Sum[i] = info.Mx_Inforces.Sum(k => k.Value[i]) + info.Mx_Inforce[i] + info.Mx_Payment[i] + info.Mx_Waiver[i];
            }
        }

        public virtual void 기수표추가생성(PVInfo info)
        {
            int x = info.MP.x;
            int n = info.MP.n;
            int m = info.MP.m;

            for (int i = 0; i < n; i++)
            {
                SetVariables(info, i);
            }

            //Lx
            foreach (var kpv in info.Rider.Inforces_State)
            {
                info.Lx_States[kpv.Key] = Lx(info.Survivor_Inforces[kpv.Key], n);
            }

            //Dx
            foreach (var kpv in info.Lx_States)
            {
                info.Dx_States[kpv.Key] = Dx(kpv.Value, info.v_Acc, n);
            }

            //Nx
            foreach (var kpv in info.Dx_States)
            {
                info.Nx_States[kpv.Key] = Nx(kpv.Value, n);
            }

            //Cx
            foreach (var kpv in info.Benefit_States)
            {
                if (info.Lx_States.ContainsKey(kpv.Key))
                {
                    info.Cx_States[kpv.Key] = Cx(info.Lx_States[kpv.Key], info.v_AccMid, kpv.Value, n);
                }
            }

            //Mx
            foreach (var kpv in info.Cx_States)
            {
                info.Mx_States[kpv.Key] = Mx(kpv.Value, n);
            }
        }


        public virtual void PVInfo계산(ModelPointTable mp)
        {
            HashSet<int> ss2 = new HashSet<int>() { 0, mp.S2 };
            HashSet<int> ss3 = new HashSet<int>() { 0, mp.S3 };
            HashSet<int> ss5 = new HashSet<int>() { 0, mp.S5 };

            foreach (int s2 in ss2)
            {
                foreach (int s3 in ss3)
                {
                    foreach (int s5 in ss5)
                    {
                        PVInfo info = new PVInfo();
                        PVInfo = info;
                        PVInfos[$"{s2}|{s3}|{s5}"] = info;

                        ModelPointTable mp_temp = mp.Clone();
                        info.MP = mp_temp;

                        mp_temp.S2 = s2;
                        mp_temp.S3 = s3;
                        mp_temp.S5 = s5;

                        if (ss3.Count == 2 && s3 == 0) mp_temp.m = Math.Min(mp_temp.n, 20);
                        if (mp.S5 > 0 && s5 == 0) mp_temp.Jong = 1;

                        SetVariables(info.MP);
                        상품정보입력(info);
                        담보정보입력(info);
                        위험률입력(info);
                        사업비입력(info);
                        기수표생성(info);
                        요율계산(info);
                        
                    }
                }
            }
        }

        public virtual void 요율계산(PVInfo c)
        {
            순보험료계산(c);
            영업보험료계산(c);
            베타순보험료계산(c);
            준비금계산(c);
            환급금계산(c);
        }

        public virtual void 순보험료계산(PVInfo c)
        {
            int x = c.MP.x;
            int n = c.MP.n;
            int m = c.MP.m;
            int m_STD = Math.Min(n, 20);
            
            //일시납순보험료
            if (c.MP.Freq == 99)
            {
                c.NP[0] = (c.Mx_Sum[0] - c.Mx_Sum[n]) / c.Dx_Inforce[0];
                return;
            }

            //기준연납순보험료
            if (c.MP.S3 == 0)
            {
                c.NP_STD = (c.Mx_Sum[0] - c.Mx_Sum[n]) / NNx(c.Nx_Payment, c.Dx_Payment, 1, 0, m_STD);
            }
            else
            {
                c.NP_STD = PVInfos[$"{c.MP.S2}|0|{c.MP.S5}"].NP[1];
            }

            //연납입횟수별 순보험료
            foreach (int freq in new int[] { 12, 6, 4, 2, 1 })
            {
                c.NP[freq] = (c.Mx_Sum[0] - c.Mx_Sum[n]) / NNx(c.Nx_Payment, c.Dx_Payment, freq, 0, m) / freq;
            }
        }

        public virtual void 베타순보험료계산(PVInfo c)
        {
            int x = c.MP.x;
            int n = c.MP.n;
            int m = c.MP.m;

            //연납입횟수별 베타순보험료
            foreach (int freq in new int[] { 12, 6, 4, 2, 1 })
            {
                if (c.Betaprime_S == 0 && c.Betaprime_P == 0)
                {
                    c.NPBeta[freq] = c.NP[freq];
                }

                if (c.Betaprime_S > 0)
                {
                    c.NPBeta[freq] = c.NP[freq] + c.Betaprime_S * (c.Nx_Inforce[m] - c.Nx_Inforce[n]) / NNx(c.Nx_Payment, c.Dx_Payment, freq, 0, m) / freq;
                }

                if (c.Betaprime_P > 0)
                {
                    c.NPBeta[freq] = c.NP[freq] + c.GP[freq] * c.Betaprime_P * (c.Nx_Inforce[m] - c.Nx_Inforce[n]) / NNx(c.Nx_Payment, c.Dx_Payment, freq, 0, m);
                }
            }
        }

        public virtual void 영업보험료계산(PVInfo c)
        {
            int x = c.MP.x;
            int n = c.MP.n;
            int m = c.MP.m;

            //일시납영업보험료
            if (c.MP.Freq == 99)
            {
                c.GP[0] = (c.NP[0] + c.Alpha_S) / (1 - c.Alpha_P - c.Beta_P - c.Gamma - c.Ce);
                return;
            }

            //납입주기별영업보험료
            foreach (int freq in new int[] { 12, 6, 4, 2, 1 })
            {
                double acq_S = (c.Alpha_S + c.NP_STD * c.Alpha_P20) / ax(c.Nx_Payment, c.Dx_Payment, freq, 0, m) / freq;
                double acq_P = c.Alpha_P / ax(c.Nx_Payment, c.Dx_Payment, freq, 0, m);

                if (c.Betaprime_S == 0 && c.Betaprime_P == 0)
                {
                    c.GP[freq] = (c.NP[freq] + acq_S) / (1 - acq_P - c.Alpha_P2 - c.Beta_P - c.Gamma - c.Ce);
                }

                if (c.Betaprime_S > 0)
                {
                    double mnt_afterPayment_S = c.Betaprime_S * (c.Nx_Inforce[m] - c.Nx_Inforce[n]) / NNx(c.Nx_Payment, c.Dx_Payment, freq, 0, m) / freq;
                    c.GP[freq] = (c.NP[freq] + mnt_afterPayment_S + acq_S)  / (1 - acq_P - c.Alpha_P2 - c.Beta_P - c.Gamma - c.Ce);
                }

                if (c.Betaprime_P > 0)
                {
                    double mnt_afterPayment_P = c.Betaprime_P * (c.Nx_Inforce[m] - c.Nx_Inforce[n]) / NNx(c.Nx_Payment, c.Dx_Payment, freq, 0, m);
                    c.GP[freq] = (c.NP[freq] + acq_S) / (1 - acq_P - mnt_afterPayment_P - c.Alpha_P2 - c.Beta_P - c.Gamma - c.Ce);
                }
            }
        }

        public virtual void 준비금계산(PVInfo c)
        {
            int x = c.MP.x;
            int n = c.MP.n;
            int m = c.MP.m;

            //일시납준비금
            if (c.MP.Freq == 99)
            {
                for (int i = 0; i < n; i++)
                {
                    c.V[i] = (c.Mx_Sum[i] - c.Mx_Sum[n]) / c.Dx_Inforce[i];
                }
                return;
            }

            //연납입횟수별 준비금
            int freq = 1;

            if (c.Betaprime_S == 0 && c.Betaprime_P == 0)
            {
                for (int i = 0; i < n; i++)
                {
                    if(i < m)
                    {
                        c.V[i] = (c.Mx_Sum[i] - c.Mx_Sum[n] - freq * c.NP[freq] * NNx(c.Nx_Payment, c.Dx_Payment, freq, i, m)) / c.Dx_Inforce[i];
                    }
                    else
                    {
                        c.V[i] = (c.Mx_Sum[i] - c.Mx_Sum[n]) / c.Dx_Inforce[i];
                    }                    
                }
            }
            if (c.Betaprime_S > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    if (i < m)
                    {
                        c.V[i] = (c.Mx_Sum[i] - c.Mx_Sum[n] + c.Betaprime_S * (c.Nx_Inforce[m] - c.Nx_Inforce[n]) - freq * c.NPBeta[freq] * NNx(c.Nx_Payment, c.Dx_Payment, freq, i, m)) / c.Dx_Inforce[i];
                    }
                    else
                    {
                        c.V[i] = (c.Mx_Sum[i] - c.Mx_Sum[n] + c.Betaprime_S * (c.Nx_Inforce[i] - c.Nx_Inforce[n])) / c.Dx_Inforce[i];
                    }
                }
            }
            if (c.Betaprime_P > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    if (i < m)
                    {
                        c.V[i] = (c.Mx_Sum[i] - c.Mx_Sum[n] + freq * c.GP[freq] * c.Betaprime_P * NNx(c.Nx_Payment, c.Dx_Payment, freq, m, n) - freq * c.NP[freq] * NNx(c.Nx_Payment, c.Dx_Payment, 12, i, m)) / c.Dx_Inforce[i];
                    }
                    else
                    {
                        c.V[i] = (c.Mx_Sum[i] - c.Mx_Sum[n] + freq * c.GP[freq] * c.Betaprime_P * NNx(c.Nx_Payment, c.Dx_Payment, freq, i, n)) / c.Dx_Inforce[i];
                    }
                }
            }
        }

        public virtual void 환급금계산(PVInfo c)
        {
            int x = c.MP.x;
            int n = c.MP.n;
            int m = c.MP.m;          
            int freq = c.MP.Freq;
            int stype = c.Rider.Stype.Evaluate();
            int channel = c.Product.Channel;

            int m_Min7 = Math.Min(m, 7);
            int n_Min20 = Math.Min(n, 20);
            MinS값입력(c);

            c.ALPHA = freq * c.Alpha_P * c.GP[freq] + c.Alpha_P20 * c.NP_STD + c.Alpha_S;

            if (stype == 1)
            {
                c.STDALPHA = 0.05 * n_Min20 * c.NP_STD + 0.45 * c.NP_STD;
            }
            else if (stype == 2)
            {
                c.STDALPHA = 0.05 * n_Min20 * c.NP_STD + 0.01 * c.MinS;
            }
            else if (stype == 3)
            {
                c.STDALPHA = 0.05 * n_Min20 * c.NP_STD + 0.15 * c.NP_STD;
            }
            else
            {
                c.STDALPHA = 0;
            }

            if (channel == 1)
            {
                c.STDALPHA = 0.7 * c.STDALPHA;
            }

            double ALPHA_Applied = Math.Min(c.ALPHA, c.STDALPHA);

            for (int i = 0; i < 7; i++)
            {
                c.DAC[i] = ALPHA_Applied * (m_Min7 - i) / m_Min7;
            }

            for (int i = 0; i < n; i++)
            {
                c.W[i] = Math.Max(c.V[i] - c.DAC[i], 0);
            }
        }

        public virtual double 정기사망위험보험료계산(PVInfo info)
        {
            int x = info.MP.x;
            int n = info.MP.n;
            int m = info.MP.m;
            int freq = 12;

            double[] death_rate = FindRiskRate(info, "정기사망");
            double[] survival_rate = death_rate.Select(k => 1 - k).ToArray();

            double[] Lx_Inforce = Lx(survival_rate, n);
            double[] Dx_Inforce = Dx(Lx_Inforce, info.v_Acc, n);
            double[] Nx_Inforce = Nx(Dx_Inforce, n);
            double[] Cx_Inforce = Cx(Lx_Inforce, info.v_AccMid, death_rate, n);
            double[] Mx_Inforce = Mx(Cx_Inforce, n);

            double NP_Term = (Mx_Inforce[0] - Mx_Inforce[n]) / NNx(Nx_Inforce, Dx_Inforce, freq, 0, m) / freq;
            return NP_Term;
        }


        public double[] Lx(double[] survivor, int n)
        {
            double[] lx = new double[PVInfo.END_AGE];

            for (int i = 0; i <= n; i++)
            {
                if (i == 0)
                {
                    lx[0] = 100000;
                }
                else
                {
                    lx[i] = lx[i - 1] * survivor[i - 1];
                }
            }

            return lx;
        }

        public double[] Dx(double[] lx, double[] vAcc, int n)
        {
            double[] dx = new double[PVInfo.END_AGE];
            for (int i = 0; i < n; i++)
            {
                dx[i] = lx[i] * vAcc[i];
            }
            return dx;
        }

        public double[] Nx(double[] dx, int n)
        {
            double[] nx = new double[PVInfo.END_AGE];

            for (int i = n ; i >= 0; i--)
            {
                if (i == n)
                {
                    nx[i] = dx[i];
                }
                else
                {
                    nx[i] = dx[i] + nx[i + 1];
                }
            }
            return nx;
        }

        public double[] Cx(double[] lx, double[] vAccMid, double[] benefits, int n)
        {
            double[] cx = new double[PVInfo.END_AGE];
            for (int i = 0; i < n; i++)
            {
                cx[i] = lx[i] * vAccMid[i] * benefits[i];
            }
            return cx;
        }

        public double[] Mx(double[] cx, int n)
        {
            double[] nx = new double[PVInfo.END_AGE];

            for (int i = n; i >= 0; i--)
            {
                if (i == n)
                {
                    nx[i] = cx[i];
                }
                else
                {
                    nx[i] = cx[i] + nx[i + 1];
                }
            }
            return nx;
        }

        public double NNx(double[] nx, double[] dx, int freq, int start, int end)
        {
            switch (freq)
            {
                case 12:
                case 6:
                case 4:
                case 2:
                case 1:
                    return nx[start] - nx[end] - (freq - 1) / (2.0 * freq) * (dx[start] - dx[end]);
                default:
                    return nx[start] - nx[end];
            }
        }

        public double ax(double[] nx, double[] dx, int freq, int start, int end)
        {
            return NNx(nx, dx, freq, start, end) / dx[start];
        }

        public double SA(double value)
        {
            double SA = (double)Variables["SA"];
            return Round(value * SA, 0);
        }


        public ModelPointTable ToModelPointTable(string line)
        {
            ModelPointTable mp = new ModelPointTable();
            string[] arr = line.Split('\t');

            mp.ProductCode = arr[0];
            mp.RiderCode = arr[1];
            mp.Jong = ToInt(arr[2]);
            mp.x = ToInt(arr[3]);
            mp.n = ToInt(arr[4]);
            mp.m = ToInt(arr[5]);
            mp.Freq = ToInt(arr[6]);
            mp.SA = ToDouble(arr[7]);
            mp.F1 = ToInt(arr[8]);
            mp.F2 = ToInt(arr[9]);
            mp.F3 = ToInt(arr[10]);
            mp.F4 = ToInt(arr[11]);
            mp.F5 = ToInt(arr[12]);
            mp.F6 = ToInt(arr[13]);
            mp.F7 = ToInt(arr[14]);
            mp.F8 = ToInt(arr[15]);
            mp.F9 = ToInt(arr[16]);
            mp.S1 = ToInt(arr[17]);
            mp.S2 = ToInt(arr[18]);
            mp.S3 = ToInt(arr[19]);
            mp.S4 = ToInt(arr[20]);
            mp.S5 = ToInt(arr[21]);
            mp.S6 = ToInt(arr[22]);
            mp.S7 = ToInt(arr[23]);
            mp.S8 = ToInt(arr[24]);
            mp.S9 = ToInt(arr[25]);

            return mp;
        }

        public ProductTable ToProductTable(string line)
        {
            ProductTable r = new ProductTable();
            string[] arr = line.Split('\t');

            r.ProductCode = arr[0];
            r.Jong = ToInt(arr[1]);
            r.ProductName = arr[2];
            r.Date = arr[3];
            r.i = PVCompiler.CompileDouble(arr[4]);
            r.ii = PVCompiler.CompileDouble(arr[5]);
            r.w = PVCompiler.CompileDouble(arr[6]);
            r.Channel = ToInt(arr[7]);

            return r;
        }

        public RiderTable ToRiderTable(string line)
        {
            RiderTable r = new RiderTable();
            string[] arr = line.Split('\t');

            r.ProductCode = arr[0];
            r.RiderCode = arr[1];
            r.Jong = ToInt(arr[2]);
            r.RiderName = arr[3];
            r.PVType = PVCompiler.CompileInt(arr[4]);
            r.Stype = PVCompiler.CompileInt(arr[5]);
            r.Inforce = PVCompiler.CompileDouble(arr[6]);
            r.Payment = PVCompiler.CompileDouble(arr[7]);

            for (int i = 0; i < 10; i++)
            {
                if (!string.IsNullOrWhiteSpace(arr[8 + i]))
                {
                    r.Benefits[i + 1] = PVCompiler.CompileDouble(arr[8 + i]);
                    r.Inforces[i + 1] = string.IsNullOrWhiteSpace(arr[18 + i]) ? r.Inforce : PVCompiler.CompileDouble(arr[18 + i]);
                }
            }

            r.Benefit_Inforce = PVCompiler.CompileDouble(arr[28]);
            r.Benefit_Payment = PVCompiler.CompileDouble(arr[29]);
            r.Benefit_Waiver = PVCompiler.CompileDouble(arr[30]);

            for (int i = 0; i < 4; i++)
            {
                if (!string.IsNullOrWhiteSpace(arr[31 + i]))
                {
                    r.Benefits_State[i + 1] = PVCompiler.CompileDouble(arr[31 + i]);
                    r.Inforces_State[i + 1] = PVCompiler.CompileDouble(arr[35 + i]);
                }
            }

            for (int i = 0; i < 30; i++)
            {
                if (!string.IsNullOrWhiteSpace(arr[39 + i]))
                {
                    r.RiskRateNameMap[i + 1] = arr[39 + i];
                }
            }

            for (int i = 0; i < 10; i++)
            {
                if (!string.IsNullOrWhiteSpace(arr[69 + i]))
                {
                    r.Parameters_r[i + 1] = PVCompiler.CompileDouble(arr[69 + i]);
                    r.Parameters_k[i + 1] = PVCompiler.CompileDouble(arr[79 + i]);
                }
            }

            return r;
        }

        public RiskRateTable ToRiskRateTable(string line)
        {
            RiskRateTable r = new RiskRateTable();
            string[] arr = line.Split('\t');

            r.RiskRateName = arr[0];
            r.F1 = ToNullableInt(arr[1]);
            r.F2 = ToNullableInt(arr[2]);
            r.F3 = ToNullableInt(arr[3]);
            r.F4 = ToNullableInt(arr[4]);
            r.F5 = ToNullableInt(arr[5]);
            r.F6 = ToNullableInt(arr[6]);
            r.F7 = ToNullableInt(arr[7]);
            r.F8 = ToNullableInt(arr[8]);
            r.F9 = ToNullableInt(arr[9]);
            r.Date = arr[10];
            r.Face = ToDouble(arr[11]);
            r.Offset = PVCompiler.CompileInt(arr[12]);
            r.RiskRates = Enumerable.Range(0, 131).Select(k => ToDouble(arr[k + 13])).ToArray();

            return r;
        }

        public ExpenseTable ToExpenseTable(string line)
        {
            ExpenseTable r = new ExpenseTable();
            string[] arr = line.Split('\t');

            r.ProductCode = arr[0];
            r.RiderCode = arr[1];
            r.Jong = ToInt(arr[2]);
            r.Condition1 = PVCompiler.CompileBool(arr[3]);
            r.Condition2 = PVCompiler.CompileBool(arr[4]);
            r.Condition3 = PVCompiler.CompileBool(arr[5]);
            r.Condition4 = PVCompiler.CompileBool(arr[6]);

            r.Alpha_S = PVCompiler.CompileDouble(arr[7]);
            r.Alpha_P = PVCompiler.CompileDouble(arr[8]);
            r.Alpha_P2 = PVCompiler.CompileDouble(arr[9]);
            r.Alpha_P20 = PVCompiler.CompileDouble(arr[10]);
            r.Beta_S = PVCompiler.CompileDouble(arr[11]);
            r.Beta_P = PVCompiler.CompileDouble(arr[12]);
            r.Betaprime_S = PVCompiler.CompileDouble(arr[13]);
            r.Betaprime_P = PVCompiler.CompileDouble(arr[14]);
            r.Gamma = PVCompiler.CompileDouble(arr[15]);
            r.Ce = PVCompiler.CompileDouble(arr[16]);
            r.Refund_P = PVCompiler.CompileDouble(arr[17]);
            r.Refund_S = PVCompiler.CompileDouble(arr[18]);
            r.Exp_etc1 = PVCompiler.CompileDouble(arr[19]);
            r.Exp_etc2 = PVCompiler.CompileDouble(arr[20]);
            r.Exp_etc3 = PVCompiler.CompileDouble(arr[21]);
            r.Exp_etc4 = PVCompiler.CompileDouble(arr[22]);

            return r;
        }

        public StandardAgeTable ToStandardAgeTable(string line)
        {
            StandardAgeTable r = new StandardAgeTable();
            string[] arr = line.Split('\t');

            r.ProductCode = arr[0];
            r.RiderCode = arr[1];
            r.Jong = ToInt(arr[2]);
            r.x = ToInt(arr[3]);
            r.n = ToInt(arr[4]);
            r.m = ToInt(arr[5]);
            r.Freq = ToInt(arr[6]);
            r.SA = ToDouble(arr[7]);
            r.F1 = ToInt(arr[8]);
            r.F2 = ToInt(arr[9]);
            r.F3 = ToInt(arr[10]);
            r.F4 = ToInt(arr[11]);
            r.F5 = ToInt(arr[12]);
            r.F6 = ToInt(arr[13]);
            r.F7 = ToInt(arr[14]);
            r.F8 = ToInt(arr[15]);
            r.F9 = ToInt(arr[16]);
            r.S1 = ToInt(arr[17]);   
            r.S2 = ToInt(arr[18]);
            r.S3 = ToInt(arr[19]);
            r.S4 = ToInt(arr[20]);
            r.S5 = ToInt(arr[21]);
            r.S6 = ToInt(arr[22]);
            r.S7 = ToInt(arr[23]);
            r.S8 = ToInt(arr[24]);
            r.S9 = ToInt(arr[25]);

            r.MinSGroup_Condition1 = PVCompiler.CompileBool(arr[26]);
            r.MinSGroup_Condition2 = PVCompiler.CompileBool(arr[27]);
            r.MinSGroup_Condition3 = PVCompiler.CompileBool(arr[28]);

            r.NP = ToDouble(arr[29]);
            r.TermNP = ToDouble(arr[30]);
            r.S = ToDouble(arr[31]);

            return r;
        }


        public void SetVariables(ModelPointTable mp)
        {
            PropertyInfo[] properties = typeof(ModelPointTable).GetProperties();

            foreach (var property in properties)
            {
                var value = property.GetValue(mp);
                Variables[property.Name] = value ?? "";
            }

            Variables["t"] = 0;
            Variables["i"] = 0.0;
            Variables["ii"] = 0.0;
            Variables["v"] = 0.0;
            Variables["vv"] = 0.0;
            Variables["w"] = 0.0;

            for (int i = 0; i < 30; i++)
            {
                Variables["q" + (i + 1)] = 0.0;
            }

            for (int i = 0; i < 10; i++)
            {
                Variables["r" + (i + 1)] = 0.0;
                Variables["k" + (i + 1)] = 0.0;
            }
        }

        public void SetVariables(PVInfo info, int t)
        {
            Variables["t"] = t;
            Variables["i"] = info.Product.i.Evaluate();
            Variables["ii"] = info.Product.ii.Evaluate(); ;
            Variables["w"] = info.MP.S5 == 0 ? 0 : info.Product.w.Evaluate();
            Variables["v"] = 1.0 / (1.0 + (double)Variables["i"]);
            Variables["vv"] = 1.0 / (1.0 + (double)Variables["ii"]);

            info.i[t] = (double)Variables["i"];
            info.ii[t] = (double)Variables["ii"];
            info.w[t] = (double)Variables["w"];
            info.v[t] = (double)Variables["v"];
            info.vv[t] = (double)Variables["vv"];

            //RiskRate, Parmeterized-Rate
            foreach (var kpv in info.RiskRates)
            {
                Variables["q" + kpv.Key] = kpv.Value[t];
            }

            foreach (var kpv in info.Rider.Parameters_r)
            {
                Variables["r" + kpv.Key] = kpv.Value.Evaluate();
            }

            foreach (var kpv in info.Rider.Parameters_k)
            {
                Variables["k" + kpv.Key] = kpv.Value.Evaluate();
            }
        }

        public virtual double[] FindRiskRate(PVInfo info, string rateName)
        {
            if (!RiskRateLookup.Contains(rateName))
            {
                throw new Exception("위험률이 존재하지 않습니다. " + rateName);
            }

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

                double[] adjustedRates = new double[PVInfo.END_AGE];
                int offset = riskrate.Offset.Evaluate();
                int idx = 0;

                int x = info.MP.x;
                double Face = riskrate.Face;

                for (int i = 0; i < PVInfo.END_AGE - x; i++)
                {
                    if (offset == 1) idx = x + i;
                    if (offset == 2) idx = i;
                    adjustedRates[i] = riskrate.RiskRates[idx] / Face;
                }

                return adjustedRates;
            }

            throw new Exception("조건에 맞는 위험률을 찾을 수 없습니다. " + rateName);
        }

        public double ToDouble(string s, double defaultVal = 0)
        {
            return double.TryParse(s, out double val) ? val : defaultVal;
        }

        public int ToInt(string s, int defaultVal = 0)
        {
            return int.TryParse(s, out int val) ? val : defaultVal;
        }

        public Nullable<int> ToNullableInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            return ToInt(s);
        }
    }

    public class PVInfo
    {
        public string PropertyKey;
        public const int END_AGE = 131; // 0~130

        public ModelPointTable MP;
        public ProductTable Product;
        public RiderTable Rider;

        //Interest Rate
        public double[] i = new double[END_AGE];
        public double[] v = new double[END_AGE];
        public double[] v_Acc = new double[END_AGE];
        public double[] v_AccMid = new double[END_AGE];
        public double[] w = new double[END_AGE];
        public double[] ii = new double[END_AGE];
        public double[] vv = new double[END_AGE];

        //RiskRate
        public Dictionary<int, double[]> RiskRates = new Dictionary<int, double[]>();

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
        public Dictionary<int, double[]> Rate_k = new Dictionary<int, double[]>();
        public Dictionary<int, double[]> Rate_r = new Dictionary<int, double[]>();

        //Benefit Rate
        public double[] Benefit_Inforce = new double[END_AGE];
        public double[] Benefit_Payment = new double[END_AGE];
        public double[] Benefit_Waiver = new double[END_AGE];
        public Dictionary<int, double[]> Benefit_Inforces = new Dictionary<int, double[]>();
        public Dictionary<int, double[]> Benefit_States = new Dictionary<int, double[]>();

        //Withrwal Rate
        public double[] Survivor_Inforce = new double[END_AGE];
        public double[] Survivor_Payment = new double[END_AGE];
        public double[] Survivor_Waiver = new double[END_AGE];
        public Dictionary<int, double[]> Survivor_Inforces = new Dictionary<int, double[]>();
        public Dictionary<int, double[]> Survivor_States = new Dictionary<int, double[]>();

        //Lx
        public double[] Lx_Inforce = new double[END_AGE];
        public double[] Lx_Payment = new double[END_AGE];
        public double[] Lx_Waiver = new double[END_AGE];
        public Dictionary<int, double[]> Lx_Inforces = new Dictionary<int, double[]>();
        public Dictionary<int, double[]> Lx_States = new Dictionary<int, double[]>();

        //Dx
        public double[] Dx_Inforce = new double[END_AGE];
        public double[] Dx_Payment = new double[END_AGE];
        public double[] Dx_Waiver = new double[END_AGE];
        public Dictionary<int, double[]> Dx_Inforces = new Dictionary<int, double[]>();
        public Dictionary<int, double[]> Dx_States = new Dictionary<int, double[]>();

        //Nx
        public double[] Nx_Inforce = new double[END_AGE];
        public double[] Nx_Payment = new double[END_AGE];
        public double[] Nx_Waiver = new double[END_AGE];
        public Dictionary<int, double[]> Nx_Inforces = new Dictionary<int, double[]>();
        public Dictionary<int, double[]> Nx_States = new Dictionary<int, double[]>();

        //Cx
        public double[] Cx_Inforce = new double[END_AGE];
        public double[] Cx_Payment = new double[END_AGE];
        public double[] Cx_Waiver = new double[END_AGE];
        public Dictionary<int, double[]> Cx_Inforces = new Dictionary<int, double[]>();
        public Dictionary<int, double[]> Cx_States = new Dictionary<int, double[]>();

        //Mx
        public double[] Mx_Inforce = new double[END_AGE];
        public double[] Mx_Payment = new double[END_AGE];
        public double[] Mx_Waiver = new double[END_AGE];
        public Dictionary<int, double[]> Mx_Inforces = new Dictionary<int, double[]>();
        public Dictionary<int, double[]> Mx_States = new Dictionary<int, double[]>();

        public double[] Mx_Sum = new double[END_AGE]; //Sum of Mx_Inforce, Mx_Payment, Mx_Waiver

        //Premium, Reserve.., Key=Freq
        //NP: 순보험료, NP_STD: 기준연납순보험료, NPBeta: 베타순보험료, GP: 영업보험료, RP: 위험보험료, YRT(Yearly Renewable Term Premium): 자연식위험보험료
        public Dictionary<int, double> NP = new Dictionary<int, double>();
        public Dictionary<int, double> NPBeta = new Dictionary<int, double>();
        public Dictionary<int, double> GP = new Dictionary<int, double>();
        public Dictionary<int, double> RP = new Dictionary<int, double>();

        public double NP_STD;
        public double ALPHA;
        public double STDALPHA;

        public double[] YRT = new double[END_AGE];
        public double[] V = new double[END_AGE];
        public double[] W = new double[END_AGE];
        public double[] DAC = new double[END_AGE];
        
        public double S;
        public double MinS;
    }

}
