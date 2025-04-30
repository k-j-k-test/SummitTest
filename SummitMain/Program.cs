using SummitActuary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SummitMain
{
    class Program
    {
        static void Main(string[] args)
        {
            RunMultiPVPricing();
        }

        static void RunSinglePVPricing()
        {
            PVPricing pricing = new PVPricing();

            pricing.S산출("60682");
            pricing.OutputFileSuffix = "1종";
            pricing.PV산출("60682", jong: 1);
            pricing.OutputFileSuffix = "2종";
            pricing.PV산출("60682", jong: 2);
        }

        static void RunMultiPVPricing()
        {
            new PVPricing().S산출("60682");

            Parallel.Invoke(
                () => {
                    var pricing1 = new PVPricing();
                    pricing1.OutputFileSuffix = "1종";
                    pricing1.PV산출("60682", jong: 1);
                },
                () => {
                    var pricing2 = new PVPricing();
                    pricing2.OutputFileSuffix = "2종";
                    pricing2.PV산출("60682", jong: 2);
                }
            );
        }
    }

    public class UnitTest
    {
        public string UnitTestDirectory { get; set; }

        public void ExpandTest()
        {
            // DataExpander<TestModelPoint> 생성
            var expander = new DataExpander<TestModelPoint>();

            // Dictionary<string, string>으로 입력값 정의
            var expressionValues = new Dictionary<string, string>
            {
                { "ProcutCode", "WL" },
                { "RiderCode", "C1, C2" },
                { "GenderCode", "01, 02" },
                { "GradeCode", "01" },
                { "x", "15~60" },
                { "n", "omega-x" },
                { "m", "10,15" },
                { "omega", "100" },
                { "SA", "10000" }
            };

            List<TestModelPoint> expandedModels = expander.ExpandData(expressionValues);
            string outPath = Path.Combine(UnitTestDirectory, "ExpandExampleResult.txt");

            using (StreamWriter sw = new StreamWriter(outPath, default))
            {
                for (int j = 0; j < expandedModels.Count; j++)
                {
                    Console.WriteLine((j + 1) + ": " + expandedModels[j].ToString());
                    sw.WriteLine(expandedModels[j].ToString());
                }
            }
        }

        public void MatchTest()
        {
            List<LTFStream> readers = new List<LTFStream>()
            {
                new LTFStream(Path.Combine(UnitTestDirectory, "LA02492_PT_1.txt"), x => x.Substring(28, 121)),
                new LTFStream(Path.Combine(UnitTestDirectory, "LA02492_PT_2.txt"), x => x.Substring(28, 121)),
            };

            List<string[]> loopBy = new List<string[]>()
            {
                 new string[] { "CLA12414", "CLA12414" },
                 new string[] { "CLA01801", "CLA01801" }
            };

            var baseDir = readers[0].CreateProcessingFolder("Matched");

            using (StreamWriter swTrue = new StreamWriter(Path.Combine(baseDir, "True.txt"), false))
            using (StreamWriter swFalse = new StreamWriter(Path.Combine(baseDir, "False.txt"), false))
            using (StreamWriter swFailed = new StreamWriter(Path.Combine(baseDir, "MatchFailed.txt"), false))
            {
                foreach (var riderCodes in loopBy)
                {
                    List<LTFStream> splitReaders = new List<LTFStream>();

                    for (int i = 0; i < readers.Count; i++)
                    {
                        string splitPath = Path.Combine(readers[i].CreateProcessingFolder("Splited"), riderCodes[i] + readers[i].Extension);
                        splitReaders.Add(new LTFStream(splitPath, readers[i].KeySelector));
                    }

                    var mathchedResult = splitReaders[0].Match(splitReaders[1]);

                    foreach (var kpv in mathchedResult)
                    {
                        string key = kpv.Key;

                        if (kpv.Value.Any(v => v.Count == 0))
                        {
                            kpv.Value[0].ForEach(line => swFailed.WriteLine(line));
                            continue;
                        }

                        List<string> value0 = kpv.Value[0];
                        List<string> value1 = kpv.Value[1];

                        double sum0 = value0.Select(line => double.Parse(line.Substring(196, 15))).Sum();
                        double sum1 = value1.Select(line => double.Parse(line.Substring(196, 15))).Sum();

                        string resultLine = string.Join("\t", key, sum0, sum1, value0.Count, value1.Count, value0.First(), value1.First());

                        if (sum0 < sum1)
                        {
                            swTrue.WriteLine(resultLine);
                        }
                        else
                        {
                            swFalse.WriteLine(resultLine);
                        }
                    }
                }
            }
        }

        public void IndexingTest()
        {
            //23m: 결산마감테이블, 04m: 입금마감테이블
            LTFStream reader_23m = new LTFStream(Path.Combine(UnitTestDirectory, "tb_23m.txt"), line => line.Split('\t')[0]) { Delimiter = "\t" };
            LTFStream reader_04m = new LTFStream(Path.Combine(UnitTestDirectory, "tb_04m.txt"), line => line.Split('\t')[1]) { Delimiter = "\t" };

            reader_23m.LoadIndex();
            reader_04m.LoadIndex();

            //동일한 증권번호로 각각의 데이터 가져오기
            string policyNum = "110757349";
            List<tb_23m> _23m = reader_23m.GetLines<tb_23m>(policyNum);
            List<tb_04m> _04m = reader_04m.GetLines<tb_04m>(policyNum);

            reader_23m.Sample(policyNum);
            reader_04m.Sample(policyNum);
        }
    }

    public class TestModelPoint
    {
        public string ProcutCode { get; set; }
        public string RiderCode { get; set; }
        public string GenderCode { get; set; }
        public string GradeCode { get; set; }
        public int x { get; set; }
        public int n { get; set; }
        public int m { get; set; }
        public int omega { get; set; }
        public double SA { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(ProcutCode);
            sb.Append('|');
            sb.Append(RiderCode);
            sb.Append('|');
            sb.Append(GenderCode);
            sb.Append('|');
            sb.Append(GradeCode);
            sb.Append('|');
            sb.Append(x);
            sb.Append('|');
            sb.Append(n);
            sb.Append('|');
            sb.Append(m);
            sb.Append('|');
            sb.Append(omega);
            sb.Append('|');
            sb.Append(SA);
            return sb.ToString();
        }
    }

    public class tb_04m
    {
        public string 마감년월 { get; set; }
        public string 증권번호 { get; set; }
        public string 보유실효구분 { get; set; }
        public int 납회 { get; set; }
        public int 납입년월 { get; set; }
        public DateTime 입금영수일자 { get; set; }
        public DateTime 수정납년월일 { get; set; }
        public string 납입주기코드 { get; set; }
        public string 수금방법코드 { get; set; }
        public double 합계원보험료 { get; set; }
        public double 수금보험료 { get; set; }
        public double 원주보험료 { get; set; }
        public double 원특약보험료 { get; set; }
        public string 보험료할인종류코드1 { get; set; }
        public double 할인보험료1 { get; set; }
        public string 보험료할인종류코드2 { get; set; }
        public double 할인보험료2 { get; set; }
        public string 보험료할인종류코드3 { get; set; }
        public double 할인보험료3 { get; set; }
        public string 보험료할인종류코드4 { get; set; }
        public double 할인보험료4 { get; set; }
        public string 보험료할인종류코드5 { get; set; }
        public double 할인보험료5 { get; set; }
        public string 입출금방법구분 { get; set; }
        public string 보험료종류구분코드 { get; set; }
        public double 연체이자 { get; set; }
        public double 선납할인보험료 { get; set; }
        public string 공란01 { get; set; }
        public string 공란02 { get; set; }
        public string 공란03 { get; set; }
        public string 공란04 { get; set; }
        public string 공란05 { get; set; }
    }

    public class tb_23m
    {
        public string 증권번호 { get; set; }
        public string 상품코드세 { get; set; }
        public string 상품코드목 { get; set; }
        public string 상태코드 { get; set; }
        public DateTime 계약일자 { get; set; }
        public DateTime 만기일자 { get; set; }
        public DateTime 소멸일자 { get; set; }
        public string 납입주기 { get; set; }
        public int 보험기간 { get; set; }
        public int 납입기간 { get; set; }
        public int 실보험기간 { get; set; }
        public int 실납입기간 { get; set; }
        public double 보험가입금액 { get; set; }
        public double 보험료 { get; set; }
        public int 주피보험자연령 { get; set; }
        public string 주피보험자성별 { get; set; }
        public string 주피보험자사망여부 { get; set; }
        public int 종피배우자연령 { get; set; }
        public string 종피배우자성별 { get; set; }
        public string 종피배우자사망여부 { get; set; }
        public int 가입자녀연령 { get; set; }
        public string 가입자녀성별 { get; set; }
        public string 가입자녀사망여부 { get; set; }
        public double 순보식적립금 { get; set; }
        public double 미상각신계약비 { get; set; }
        public double 해약식적립금 { get; set; }
        public double 미경과보험료 { get; set; }
        public int 경과기간년 { get; set; }
        public int 경과기간월 { get; set; }
        public int 경과기간일 { get; set; }
        public int 미경과개월수 { get; set; }
        public int 총상각기간 { get; set; }
        public string 적립금구분 { get; set; }
        public string 참조상품코드세 { get; set; }
        public string 참조상품코드목 { get; set; }
        public string 구분적립코드 { get; set; }
        public string 사용항목1 { get; set; }
        public string 사용항목2 { get; set; }
        public string 사용항목3 { get; set; }
        public string 사용항목4 { get; set; }
        public string 사용항목5 { get; set; }
        public string 사용항목6 { get; set; }
        public string 사용항목7 { get; set; }
        public string 사용항목8 { get; set; }
        public string 사용항목9 { get; set; }
        public string 사용항목10 { get; set; }
        public string 적립금기준종류 { get; set; }
        public string 적립금기준금액 { get; set; }
        public double 순보험료 { get; set; }
        public double 예정신계약비 { get; set; }
        public double 기시적립금 { get; set; }
        public double 기말적립금 { get; set; }
        public string 적용이율코드 { get; set; }
        public string 기본순보험료적립액 { get; set; }
        public string 증액적립액 { get; set; }
        public string 추가납입적립액 { get; set; }
        public string 마감년월 { get; set; }
        public string 증권번호2 { get; set; }
        public string 보유실효구분 { get; set; }
        public string 결산상태코드 { get; set; }
        public string 계약상태코드 { get; set; }
        public string 계약상태상세코드 { get; set; }
        public string 월보유형코드 { get; set; }
        public string 대표상품코드 { get; set; }
        public string 보험종류코드세 { get; set; }
        public string 보험종류코드목 { get; set; }
        public string 상품코드세2 { get; set; }
        public string 상품코드목2 { get; set; }
        public string 상품관계코드 { get; set; }
        public string 가입상품번호 { get; set; }
        public string 가입상품이력번호 { get; set; }
        public double 순보식적립금2 { get; set; }
        public double 해약식적립금2 { get; set; }
        public double 상각대상순보식적립금 { get; set; }
        public double 상각대상해약식적립금 { get; set; }
        public double 비상각대상원리합계 { get; set; }
        public double 지급금원리합계 { get; set; }
        public double 최고이율상각대상순보식적립금 { get; set; }
        public double 최고이율상각대상해약식적립금 { get; set; }
        public double 최고이율비상각대상원리합계 { get; set; }
        public double 최고이율지급금원리합계 { get; set; }
        public double 적용이율상각대상순보식적립금 { get; set; }
        public double 적용이율상각대상해약식적립금 { get; set; }
        public double 적용이율비상각대상원리합계 { get; set; }
        public double 적용이율지급금원리합계 { get; set; }
        public double 신계약상각액 { get; set; }
        public int 경과개월 { get; set; }
        public double 신계약비상각합산여부 { get; set; }
        public double 이율 { get; set; }
        public double 부리이율 { get; set; }
        public double 보험료2 { get; set; }
        public double 가입금액 { get; set; }
        public int 연금지급개시연령 { get; set; }
        public string 연금지급개시일자 { get; set; }
        public string 연금지급형태코드 { get; set; }
        public string 확정보증지급기간 { get; set; }
        public string 연금지급주기 { get; set; }
        public string 계약전환일자 { get; set; }
        public string 최종납입일자 { get; set; }
        public int 최종납입회차 { get; set; }
        public int 최종응당년월 { get; set; }
        public int 최종종납년월 { get; set; }
        public int 주피보험자연령2 { get; set; }
        public string 주피보험자성별2 { get; set; }
        public string 최초계약일자 { get; set; }
        public string 계약일자2 { get; set; }
        public DateTime 계약만기일자 { get; set; }
        public DateTime 소멸일자2 { get; set; }
        public DateTime 실효일자 { get; set; }
        public string 납입주기2 { get; set; }
        public string 실납입기간2 { get; set; }
        public string 실보험기간2 { get; set; }
        public string 잔존기간 { get; set; }
        public string 완납후유지비처리여부 { get; set; }
        public string 중도해지이율코드 { get; set; }
        public string 최고부리이율코드 { get; set; }
        public string 질병1급장해생사구분 { get; set; }
        public string 재해1급장해생사구분 { get; set; }
        public string 위험보험료 { get; set; }
        public string 순보험료산출방법 { get; set; }
        public string 마지막회차순보험료 { get; set; }
        public string 마지막회차대체보험료 { get; set; }
        public double 총신계약비 { get; set; }
        public double 총신계약비적용 { get; set; }
        public string 미상각기간 { get; set; }
        public string 할증보험료적립금 { get; set; }
        public string 실효부리원금 { get; set; }
        public string 기납입보험료 { get; set; }
        public string 적립구분 { get; set; }
        public string 회사구분 { get; set; }
        public string 전기납여부 { get; set; }
        public string 한정납회 { get; set; }
        public string 현대가입상품사용여부 { get; set; }
        public string 가입상품갱신경과년수 { get; set; }
        public string 갱신일자 { get; set; }
        public string 증액보험료원리합계 { get; set; }
        public string 추가납입원리합계 { get; set; }
        public string 대체보험료원리합계 { get; set; }
        public string 년중도급부원리합계 { get; set; }
        public string 월중도급부원리합계 { get; set; }
        public string 중도인출원리합계 { get; set; }
        public string 최종회차연금연액 { get; set; }
        public string 완납후유지비원리합계 { get; set; }
        public string 위험보험료원리합계 { get; set; }
        public string 특약보험료원리합계 { get; set; }
        public string 증액부가여부 { get; set; }
        public string 자유납입부가여부 { get; set; }
        public string 중도인출가능여부 { get; set; }
        public string 년중도급부발생여부 { get; set; }
        public string 월중도급부발생여부 { get; set; }
        public string 납입면제적용여부 { get; set; }
        public string 지수연계상품형태 { get; set; }
        public string 보험료완납해당년월 { get; set; }
        public double 적용순보식적립금 { get; set; }
        public double 표준순보식적립금 { get; set; }
        public double 적용해약식적립금 { get; set; }
        public double 표준해약식적립금 { get; set; }
        public double 최저보증이율 { get; set; }
        public double 신계약비 { get; set; }
        public double 일시납기준예정적립금 { get; set; }
        public double 변동실적립금 { get; set; }
        public double 변동예정적립금 { get; set; }
        public double 변동보험금 { get; set; }
        public double 선납이자 { get; set; }
        public double 보너스적립금 { get; set; }
        public double 생활자금보너스준비금 { get; set; }
        public double 납입보너스준비금 { get; set; }
        public double 사망차액준비금 { get; set; }
        public double 변액보험예정적립금 { get; set; }
        public double 변액보험예정추가납입적립금 { get; set; }
        public double 변액보험일시납예정적립금 { get; set; }
    }
}