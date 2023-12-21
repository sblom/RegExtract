using System;

using Xunit;

using System.Text.RegularExpressions;
using System.Collections.Generic;
using Xunit.Abstractions;
using RegExtract.RegexTools;

namespace RegExtract.Test
{
    public class Usage
    {
        private readonly ITestOutputHelper output;

        public Usage(ITestOutputHelper output)
        {
            this.output = output;
        }

        const string data = "123456789";
        const string pattern = "(.)(.)(.)(.)(.)(.)(.)(.)(.)";
        const string pattern_nested = "(((.)(.)(.)(.)(.)(.)(.)(.)(.)))";
        const string pattern_named = "(?<n>(?<s>(?<a>.)(?<b>.)(?<c>.)(?<d>.)(?<e>.)(?<f>.)(?<g>.)(?<h>.)(?<i>.)))";

        [Fact]
        public void a001()
        {
            var plan = CreateAndLogPlan<List<(char, char)>>(/* language=regex */@"((\w)(\w))+");

            var result = plan.Extract( @"abcdef");

            Assert.Equal(result, [('a', 'b'), ('c', 'd'), ('e', 'f')]);
        }

        [Fact]
        public void a002()
        {
            var plan = CreateAndLogPlan<List<int>>(/* language=regex */@"((\d+) ?)+");
            
            var result = plan.Extract(@"123 456 789");

            Assert.Equal(result, [123, 456, 789]);
        }

        record game(int id, List<draw> draws);
        record draw(List<(int count, string color)> colors);

        [Fact]
        public void a003()
        {
            var plan = CreateAndLogPlan<game>(/* language=regex */@"Game (\d+): (((\d+) (\w+),? ?)+;? ?)+");
            
            var result = plan.Extract("Game 31: 9 blue, 6 red, 7 green; 20 red, 1 green, 15 blue");

            Assert.Equivalent(result, new game(31, [new draw([(9, "blue"), (6, "red"), (7, "green")]), new draw([(20, "red"), (1, "green"), (15, "blue")])]));
        }

        [Fact]
        public void a004()
        {
            var plan = CreateAndLogPlan<List<(char, int)>>(/* language=regex */@"(([RL])(\d+),? ?)+");

            var result = plan.Extract("R8, R4, L4, R8");

            Assert.Equal(result, [('R', 8), ('R', 4), ('L', 4), ('R', 8)]);
        }


        [Fact]
        public void a005()
        {
            var plan = CreateAndLogPlan<Dictionary<string, (string left, string right)>>(/* language=regex */@"((...) = \(((...), (...))\);? ?)+");

            var result = plan.Extract(@"AAA = (BBB, CCC); BBB = (DDD, EEE)");

            var expected = new Dictionary<string, (string left, string right)>
            {
                ["AAA"] = ("BBB", "CCC"),
                ["BBB"] = ("DDD", "EEE")
            };

            Assert.Equal(expected, result);
        }

        [Fact]
        public void a006()
        {
            var plan = CreateAndLogPlan<List<int>>(/* language=regex */@"((\d+) ?)+");

            var result = plan.Extract("123 456 789");

            Assert.Equal([123, 456, 789], result);
        }

        [Fact]
        public void a007()
        {
            var plan = CreateAndLogPlan<List<int>>(/* language=regex */@"(?:(\d+) ?)+");

            var result = plan.Extract("123 456 789");

            Assert.Equal([123, 456, 789], result);
        }

        [Fact]
        public void a008()
        {
            var plan = CreateAndLogPlan<List<int>>(/* language=regex */@"(\d+ ?)+");

            var result = plan.Extract("123 456 789");

            Assert.Equal([123, 456, 789], result);
        }

        [Fact]
        public void a009()
        {
            var plan = CreateAndLogPlan<List<(string, string)>>(/* language=regex */@"(([a-z]+)([=-][0-9]?),?)+");

            var result = plan.Extract(@"rn=1,cm-,qp=3,cm=2,qp-,pc=4,ot=9,ab=5,pc-,pc=6,ot=7");

            Assert.Equal([("rn", "=1"), ("cm", "-"), ("qp", "=3"), ("cm", "=2"), ("qp", "-"), ("pc", "=4"), ("ot", "=9"), ("ab", "=5"), ("pc", "-"), ("pc", "=6"), ("ot", "=7")], result);
        }

        [Fact]
        public void a010()
        {
            var plan = CreateAndLogPlan<List<List<List<char>>>>(/* language=regex */@"(((\w)+ ?)+,? ?)+");

            var result = plan.Extract(@"asdf lkj, wero oiu");

            Assert.Equal([[['a', 's', 'd', 'f'], ['l', 'k', 'j']], [['w', 'e', 'r', 'o'], ['o', 'i', 'u']]], result);
        }

        [Fact]
        public void a011()
        {
            var plan = CreateAndLogPlan<((char? type, string name) module, List<string> outputs)>(/* language=regex */@"^(([%&])?([a-z]+)) -> (([a-z]+),? ?)+$");

            var result = plan.Extract("&kx -> zs, br, jd, bj, vg");

            Assert.Equivalent((('&', "kx"), new List<string>() { "zs", "br", "jd", "bj", "vg"}), result);
        }

        record Rule()
        {
            public static Rule? Parse(string str)
            {
                if (str.Contains(":"))
                    return str.Extract<Conditional>();
                else
                    return str.Extract<Absolute>();
            }
        }

        record Absolute(Action step) : Rule
        {
            public const string REGEXTRACT_REGEX_PATTERN = @"(.*)";
        }
        record Conditional(Condition cond, Action step) : Rule
        {
            public const string REGEXTRACT_REGEX_PATTERN = @"((.)([<>])(\d+)):(\w+)";
        }
        record Condition(char prop, char test, int val);

        record Action
        {
            public static Action Parse(string str)
            {
                return str switch
                {
                    "A" => new Accept(),
                    "R" => new Reject(),
                    _ => new Workflow(str)
                };
            }
        }
        record Accept : Action;
        record Reject : Action;
        record Workflow(string workflow) : Action;

        [Fact]
        public void a012()
        {
            var plan = CreateAndLogPlan<(string workflow, List<Rule> rules) >(/* language=regex */@"(\w+){(([^,]+),?)+}");

            var result = plan.Extract("sxc{x>2414:jtp,s>954:R,m>2406:A,xfz}");

            Assert.Equivalent(("sxc", new List<Rule> { new Conditional(new Condition('x', '>', 2414), new Workflow("jtp")), new Conditional(new Condition('s', '>', 954), new Reject()), new Conditional(new Condition('m', '>', 2406), new Accept()), new Absolute(new Workflow("xfz")) }), result);
        }

        [Fact]
        public void a013()
        {
            var 
            tree = new RegexCaptureGroupTree(new Regex("(asdf){}"));
            output.WriteLine(tree.TreeViz());
            tree = new RegexCaptureGroupTree(new Regex(@"(asdf){01"));
            output.WriteLine(tree.TreeViz());
            tree = new RegexCaptureGroupTree(new Regex(@"(asdf){01}"));
            output.WriteLine(tree.TreeViz());
            tree = new RegexCaptureGroupTree(new Regex(@"(asdf){01,}"));
            output.WriteLine(tree.TreeViz());
            tree = new RegexCaptureGroupTree(new Regex(@"(asdf){1,2}"));
            output.WriteLine(tree.TreeViz());
            tree = new RegexCaptureGroupTree(new Regex(@"(asdf){1,2}?"));
            output.WriteLine(tree.TreeViz());
            tree = new RegexCaptureGroupTree(new Regex(@"(asdf){,12}"));
            output.WriteLine(tree.TreeViz());
            tree = new RegexCaptureGroupTree(new Regex(@"(asdf){1\,2}"));
            output.WriteLine(tree.TreeViz());
            tree = new RegexCaptureGroupTree(new Regex(@"(asdf){\1,2}"));
            output.WriteLine(tree.TreeViz());
            tree = new RegexCaptureGroupTree(new Regex(@"(asdf)\{1}"));
            output.WriteLine(tree.TreeViz());
            tree = new RegexCaptureGroupTree(new Regex(@"(asdf){1\}"));
            output.WriteLine(tree.TreeViz());
            tree = new RegexCaptureGroupTree(new Regex(@"(asdf){,}"));
            output.WriteLine(tree.TreeViz());
        }

        [Fact]
        public void slow()
        {
            var plan = ExtractionPlan<List<(string, string)>>.CreatePlan(new Regex(/* language=regex */@"(([a-z]+)([=-].?),?)+"));

            var result = plan.Extract(@"tp-,pnm-,vrg-,nfk=6,qqc=2,zs=2,hm-,xnl=4,fszj=6,cx=7,zpfsz-,hdg=9,nqqm-,nfk-,pb-,cbn=5,ng=8,pdk=4,rd=7,sl=2,qtk=7,fkdp-,dxkx-,qc=9,dxtz-,bgzmkc-,jzv-,ljd-,nbh=7,dtr-,sbd=5,zt=2,zgj-,rbpq=7,vm=6,gq=3,qnjfm-,hxd=8,rsnbql=4,bdx=7,mvgfg=8,hv=4,ql=5,tnkt-,qdh-,dv-,dqclp-,tnc=2,vgk=7,rb=6,flzg=6,rv=5,qn-,fgdr-,gj=5,gg-,vtm-,btjv=5,xs-,rfbn-,hxd=6,bhxnh-,zr-,llq=7,ds-,xs-,hdvdp=1,ll=9,kgm=1,tj-,lmx=3,mn-,zbb-,xj-,pcv=1,xrl=7,mc=2,zf=3,rrl=7,mztj=2,pcp-,fvjj=6,tn=9,jc-,xsr=2,kkgj-,bnzq=7,vtld-,hx-,zs=5,fl-,pmjz-,hh=4,hrzhq-,xs=2,dsqgh-,qqz=9,glmz=1,xs-,dvc=1,ng-,qcf-,tnsmf-,nqqm-,ksr-,kq=8,lbng=4,fntl-,zr=8,ll=1,ml=8,ctz-,hq=4,cb=1,kjq=8,ckr=4,td=8,cp=6,gpmx-,ppd=5,nc=6,mz-,dtjxv-,hmt-,vrrqpg=2,tnc-,pm=3,qqz=3,llphh=9,pm=2,kmmvl-,jl-,hmt-,tlhm=3,bh=7,kxb=2,rt-,hsm-,crqn-,mq-,mljpt=8,pcv=5,ksr-,sb=5,hprc=4,dsg-,klpm=9,qqc=8,rb=2,ntbjb-,xlc=9,rkd=3,tpfrc-,dsg=4,nbd-,sb=8,sp-,jqhkf=3,dhb=8,llq=4,jxp=7,mtr-,zlc=7,rs=9,xlg=7,qk=8,bs=2,pldx-,lrqrhk=1,lkbj=4,kmmvl-,jcj=9,qxg-,nng-,smf-,ss-,sk=2,td-,rfcgxr=1,sjfx-,kcv=9,llq=2,rxkp=4,njh-,pg=1,nf=9,ksl-,kmmvl=8,tgsdsb=6,tnc-,qkx-,hrrmd-,hrzhq=7,qnjfm=3,pc-,pg-,czh-,lsl-,mp-,sk=6,qkl-,bnzq=6,fps-,ckr=4,pcp=8,qkx-,lj-,qxg=4,bd-,mqr=5,htm-,hl-,mtkbz=4,kmmvl-,hb-,rqc-,qtk=6,mfr=9,hmt=7,nckm-,jf-,dl=7,hxd=7,mzn=5,zpfsz=1,hd-,nsl-,dx=8,ts-,gq=6,fp=3,qtk=9,fntc=9,tkxd-,bf-,dc=8,rr=2,ktqgm-,mqg=5,tnc-,sp=4,kqkl-,xvx-,zpfsz-,fsb=3,hprc=7,llh=8,xf-,bknq=5,kbvd=8,nklnq=8,flzg-,jl=3,stldk=5,ns=7,lj-,svg=3,ftdll=9,qdjff=7,pdk=9,cgb=4,cfj-,hff=7,sh=1,lj=9,mdzc=3,cdh-,spv=9,glmz=2,mzn-,zv=9,hff-,jfjgt-,qtk=3,hb-,lmdg=8,cntd-,xdfb=3,ssv-,xj-,hs-,tdvm-,fj-,rt-,gtf-,fndhj-,nknrvm-,qbp=8,nhgz-,rbzlx=7,jf-,rb=2,hhhx=1,jf-,zpn-,tk=5,mks-,sk=5,hdvdp=2,fvjj-,bhxnh-,pzz-,jfjgt-,zlc-,tnsmf-,xh-,kjq-,bpf-,rs=9,jb=7,qbms=8,rcb-,tgg-,gltc=2,pzz=3,xvx=7,hrrmd-,kqm-,xdpj-,fps=8,qqz-,tp=1,jhs=8,bpf-,dvc-,ljd-,xgmft-,dv-,ntbjb=1,ld-,bgzmkc=1,qcf=3,tnr=3,xrl-,zkknr=9,pbnr=7,hprc-,pbnr-,xfb=6,nc=5,hjq=3,bdz-,mfbj-,xl=3,vrrqpg-,ht-,mr-,bp=1,zgt=8,pb=8,dvc=1,xs=3,lmx-,kqm-,fk=5,fptk-,kd-,glmz-,nng-,jjfl-,nckm=3,sb=9,nknrvm-,hqngsj=4,fx-,xj=2,zpfsz=5,qm=3,xmd-,pnm-,sgmp=1,phl-,cmsf=4,hmt-,fp=7,kfhn-,jc-,qqc=8,pl=1,zpn-,kjq-,cmsf-,lsl-,kx=3,bnzq=7,kvjm-,rxkp=9,xnfpt=1,sn=1,qx=3,fntl-,czj=4,dpq-,mqh-,gmt=5,lcv=8,kd=9,gg=9,dkp-,nbk=8,xpt-,cncd=3,fszj-,mc=1,hc-,dkzsqb=3,dv=4,nc-,sb-,gl=1,njkr-,df-,dv=4,ltp=9,jx=2,dx=1,gl-,fgdr-,dqclp=1,dsqgh-,vtm-,vkk-,hgxd-,pqgmnl=3,nfk=5,lj-,cfj-,kt=4,mqh-,vb-,xhblcq=1,cbvr-,nf-,pk=1,lbp-,cbbt=4,lkbj=4,sdv-,jcj-,qggxgx-,dhb=3,vrrqpg-,dhb-,mbj-,mztj-,sbd=9,jbf=6,jrqk=5,nch=6,njkr=7,kqkl-,ctz-,cbn=8,klpm-,qzts=3,sgmp-,gql-,fgdk=4,lrnz-,qkx=5,pdk-,hsm-,lmx=4,fx=7,tmfn=4,jp-,fbn=9,qvxv=8,spv=2,knf=7,nxxhk=6,hprc-,km=1,jh=3,rx=7,qzgb=3,ns-,cbv=3,kqkl=3,pf-,xrlqx-,hq=4,hb=4,hc=7,qk-,nc-,dsg=6,tvd-,sp=5,ln=2,tj-,llphh=4,ztgx-,jtc-,pf-,ktqgm-,cbvr-,pm=2,xdgcfh-,gq-,rs-,xpj-,vvt=4,tkm=9,fgz=1,xvx-,tk=6,jbf=2,mvgfg=9,rsnbql-,xh-,zxdj-,kcv-,rfcgxr=9,jf=8,rv=2,sjfx=9,gb-,ptn=5,czh-,cntd=6,tvd-,gdlq=1,rb=4,nbk-,vxzxc-,lc-,xdgcfh=1,jh-,pbnr-,rt-,hdvdp=9,jhf=4,vmh=9,dhb-,xdgcfh=3,hnb=5,xpj-,fjg-,gltc-,ll-,qh-,nf=1,bnzq=4,hm-,tp=1,gl-,gq-,nbh-,dxdxl-,tvd-,cfj-,kmcgcr-,mql=4,rf=8,kcv=9,dh=1,nhgz-,xpj=3,ql-,bpf=9,jqh=7,mnmshn=5,lj-,vvl=3,jrqk=2,gdlq=1,jf=9,rcb=3,tsv=8,gnpz-,nbk=8,pc-,pf=8,lkt-,hs-,hj-,mc=1,sc=4,qcrvs-,qh-,hdvdp-,kd=8,fj=4,gql-,mvgfg-,jz=2,rt=9,qqc-,jhs-,kk=1,smf-,lbng=8,tkl-,lkmt-,tmfn=2,ljd=7,pqgmnl=9,mks-,krsqtb=1,njfx-,hh=8,sl=3,kdn=8,pldx=4,sp-,kvjm=9,mfnkh=8,qjh=8,pmjz-,tj=1,qbp-,gg-,kxb=4,tk-,htm=6,bqv-,bs=5,qvd-,qh-,kt-,zlc-,pldx-,hc-,mdjn-,hsm-,nscnp-,cbvr-,sk=2,rf=1,vpk-,dxkx=1,rrl=8,fdf=1,cfj=5,dc-,pg-,bdz=9,vb=1,jjfl=2,gg=4,dzrg-,cfj-,xb-,dl-,mc=9,xrl=8,cdh=3,rsnbql=1,jhf=1,qnjfm=3,ft=3,qdjff-,fvjj-,vd=8,qdjff=9,lkt-,htm=6,gl=6,gpmx=7,fgdr=4,mf-,sc=1,bb-,bd=6,xz-,jx-,kdn=4,ng=8,cbbt=3,xlf-,kf=7,kr=6,qqc=5,jrqk-,gnpz-,sqz-,mvgfg-,xdpj=4,xdgcfh=6,xd=5,rfbn-,qxg=1,jjhd=5,ht=4,px-,kf-,vrrqpg-,rv-,glmz-,dzrg=5,kr-,nckm-,qggxgx-,hdg-,pc=7,nc-,cm-,vmh-,tnkt=3,zlc=8,mrq-,ddf=1,xlg=1,xmd=1,dc-,hm=5,qxg-,vmcq=9,sb=5,mz-,jj=9,bpf-,ng=8,llphh-,hsm-,rrl=6,gtf-,ptn=9,dtjxv=2,jfjgt=2,gltc-,hsblgj-,hrzhq-,xdfb-,hmbhhr=5,cr=6,lxzg-,kdn=1,kvtcc-,hd=7,kxmpd=4,zfh=5,rcb-,bf=1,nqqm=4,jh-,vb-,mztj=3,zh-,cx-,qjh-,kcv-,hmt=5,hmbhhr-,lkbj-,xj=5,xfb-,cbn-,bf-,jxxhbv-,tkl-,bf-,llq-,kqkl-,tnc=3,mztj-,hsm=4,sctzlm=4,llq=2,fvjj-,zbrq-,mtkbz=4,rrv-,mfbj-,dvc=3,xd=4,zgj=5,nvc=4,hqngsj-,hl=3,cdr-,dpp-,rf-,gl-,dtr-,jqh=4,gpz-,pcp=8,kt=7,ltp-,tn=7,ng-,nbq=8,tnr-,kd-,kgm-,fsb-,xfb-,hmbhhr=5,nscnp=6,rqc=6,bb-,vgk=4,lsr-,mnmshn-,fl=6,qcf-,tvd=9,zxm-,tpfrc=3,xmd=8,kjzsmv=8,tn-,ll=8,ht=2,xg-,kvjm=8,fszj=8,sh=4,fgdr-,jqhkf-,kxmpd-,mdjn-,hg=2,qtk=7,rd=3,rqbz-,ktz-,stldk=2,xsr=2,ds=3,cbv=1,vpk-,dsqgh=2,mdzc-,dc-,ml=7,rsnbql=2,xb=4,tg-,tkl=1,rbzlx=2,jcj=6,sh=8,xm=1,nbh=2,jp-,qjv-,hv=4,ss-,cncd=1,ssv=4,glmz-,jfjgt=4,lc=7,mbj=1,xhgt-,pm-,sc-,cr-,pb=6,ss-,gmt-,qvxv=8,sgmp-,hxr-,pzz=5,qvxv=4,fl=6,zbb-,nlmj=6,kjq=4,bb=3,sb=2,srn=8,bdx=8,rsnbql-,kk-,cr=7,lj=6,ssv=4,tsv=1,bdz=5,zfh-,sctzlm-,mbj=8,hc=2,llq=1,kp=2,hhhx=2,xfb-,hm=2,sp-,rbzlx=3,dzrg=8,bqv-,mks-,xpj-,rf=2,zv-,nsl-,fgdk-,cr-,lkt=7,zqsd-,kthg-,hhhx-,clt-,bdx=6,rf=7,hs-,svg-,cf=6,ml-,sbd=6,hq=9,mp=2,lmx-,xsr-,rbzlx=8,jfjgt=3,nxxhk=2,cdr=4,ktz=4,hbp-,sp-,bm-,jqh-,sc-,btjv-,xhblcq=8,sh=5,kthg=4,ns=4,mbd=9,hbp-,pdk-,zpfsz-,zbrq-,nscnp-,hg-,ss-,pl-,kt-,kjq=5,nxxhk-,xhblcq-,vrrqpg-,gdlq-,gltc-,lv=8,dsqgh-,jl-,jjhd-,sndpd=7,mfg-,klq=4,flzg=1,nbd=2,dc=9,ktqgm-,vs=5,xs-,hrrmd-,ld-,hhhx=1,zgt-,rfcgxr=8,jxp-,hnb=4,lsr=1,mnmshn-,svg=5,pc-,kgq-,lnh-,hgxd-,kfhn=7,vvt-,zfh-,nscnp-,vgk=3,cqggjb=6,td-,mfnkh=1,mljpt-,rd-,dkzsqb-,mz=9,xlg=8,gpz-,flzg=2,zz-,fps-,llh-,fhp=7,jcj-,nch-,fps-,clt=1,nng=5,jz=3,zfxmb-,df-,rqc-,hj=8,zxdj=2,dv-,spv=1,fszj-,kcv-,hff=2,vck-,tvd-,gb=8,hv-,hsm=1,tkl-,hrzhq-,xpj-,kvtcc=3,jzd=3,cx-,xm=4,lnh=3,dltvb-,xvx-,llh-,zpn-,hc=2,cnq=2,dtjxv=5,zs=5,gdlq=7,mc-,zgj-,jc=9,kxmpd-,bf=7,sctzlm=1,hs=9,zv=2,vst-,rs-,cbvr=4,pm=4,ns=5,hm-,vb=3,nscnp-,ztgx-,jrqk=9,ddf=3,hx=3,hdg-,qcrvs=3,bfq-,fk-,qn=9,lrz-,hs-,rp=2,dj=9,cf-,pt=2,cbbt-,rt-,zkknr=9,qm=4,fndhj-,vm-,bs-,dh=7,srn=1,vmh=7,hsm-,fl-,ds-,rmdlr=6,hmbhhr=7,mvgfg-,mdzc-,hprc-,nvp=2,vmh=4,jgp=2,jqh-,nch=7,gpz=1,fhp-,bp=2,btjv-,hqngsj-,zct-,ckr=4,ltp=1,fszj=4,zpn-,xlg=8,lbng-,zfh-,jhf-,kgm=4,vrg=4,vpk-,zgj-,zxdj-,xgmft=7,hs-,gdm=2,tt-,lkt-,qdjff-,vs-,mbd=3,nng=7,clt-,dr=8,kcv-,mrq=4,ftdll-,kvtcc-,fk=7,fnqkh-,gg-,mzn=6,fszj=6,ns-,mrq-,bgzmkc-,srn=6,cp=5,hkbf-,lmdg-,fhp-,pbnr-,sndpd-,tkpp=2,kt-,ll=2,xpt=4,fqj=9,flzg-,mzn=7,lrt-,nsl=6,cmsf-,vtm-,xdqd=8,bb=1,njfx-,sgmp=9,nbd=3,hb-,jgts=6,mn=4,jcj-,pmlhn-,bpf=2,sctzlm-,gdm=6,czj=4,bnzq-,dv-,jhf-,njh=2,vfk-,hs=5,rk=8,pdk-,fntl-,cm=2,lkbj=2,ljd-,jqhkf=6,dsz-,rfbn=2,sdnmh-,kmcgcr=3,srn=8,ssv=9,mrq-,mbj-,fbhzcd-,rb=4,mqr=5,fgz-,qdjff-,jpk-,svg-,gq=8,xdfb-,hx=9,rt=8,bgzmkc=9,glnl=7,jgts-,xpj=4,xlf-,bq-,tkxd=4,lsl-,ngfn-,ts=9,jbf-,kh=8,tlhm-,klq=9,rfbn=2,xfb-,pm=9,ngfn=1,nn=5,tj-,cm=3,rg-,xlc=4,mljpt-,vtm-,tnr-,vxzxc-,ltp=7,cb-,rcb-,srn=6,mz=6,qggxgx-,tn=3,bdz-,hxr=2,fkdp-,qqz=6,xsk-,bh=5,jbf=8,bxmz=3,lsr-,cf-,qkx=2,zbb-,pnm-,fgz-,dkp=7,gb-,kxb-,rf=2,zpn=5,mfr-,rf=2,bknq-,dv-,tkxd-,pcv=8,vb-,mc-,klq=2,fk=6,stldk=1,ln=2,qc-,tt=5,vck=9,xvx-,rk=5,fp=5,ssv=9,pc-,nvp=8,xgnr=1,xhgt=2,mtr-,pmlhn-,fdf-,fjg=4,cbbt=6,vtld-,cbvr-,hm=2,ts=7,sh-,cb=8,kthg=7,tkl=7,bknq-,vst-,cmsf-,jgts=7,vgk-,vgk=9,qkx-,kp-,ktqgm=6,nfk=9,xlg=6,tj-,bpf-,fntc=2,ktz-,zbb-,bm=6,ql=4,pkdf=7,ppd=4,jz=2,pcv=2,qtk-,lrnz=2,qk-,jj=4,jfjgt=1,bfq=3,xb-,mf-,hgxd=7,lrt-,rk-,zqsd=2,gxkv=4,qn-,lmx-,sn=3,ptn=2,vpk-,qjh-,ddf-,cr-,xj-,qqc=5,zc-,hqngsj-,xrlqx=4,hnb=1,gm-,klpm-,qdjff=3,zpn=1,gql-,rqbz=4,knf-,ql-,xhgt-,cr=3,kq-,gql-,fp-,gpmx=5,ll-,xm-,tgsdsb-,zc-,zb-,mqg-,knf=9,tgg-,dj-,kkgj-,rkd-,crqn-,kqm=1,jzbv=7,qqc=9,gjtf=9,htm=9,jhs-,pg-,cf-,kvjm-,dx-,xdgcfh=3,qk-,ql=4,fgdr=4,rfcgxr=5,jgp=6,jp=2,xd=1,tn=4,qjv=4,fkdp-,pmlhn-,bknq=2,jclf=2,cdh-,dl=1,nxxhk-,zpfsz-,nbd=7,jxp-,hdg=2,km-,nsf-,xmd=8,nxxhk=3,mf-,mrq-,sgmp=3,xhgt-,pkdf-,mdzc=7,qqc=4,njh=6,ds=6,ng=1,qc=3,tgsdsb=5,dxtz=6,qvd-,qdjff-,jgts-,kq=8,phl=4,mvs-,vmcq-,qbms=8,jzd-,tkm=8,zxdj=7,qn=6,mn=5,bd=6,mvgfg=2,cgb=6,xpj-,rb=2,kfhn=4,mkvj-,gpz-,zkknr=6,qjh-,jxp=8,gm-,hprc=8,vmcq-,vmcq=1,hxr-,gdm=8,jzbv=7,njkr=2,krsqtb-,rrl-,cfj=5,dpp-,lcv=2,qjv=2,qtk-,qc=9,fx-,jqhkf=3,tn=3,jhf-,df-,ht=6,hd-,hsblgj=3,hsm=5,sjn=6,qx=3,mljpt-,bb=8,pm=1,ktz-,hd=9,gxkv-,ktqgm-,pcp=9,sndpd-,nng=3,mqh=9,pcv=1,cm-,xs-,hsm-,xpj-,nbq=1,qvd=7,vkk=2,bpf-,rmdlr-,ln-,xrl-,rfcgxr=1,qcf=4,fj-,mks-,rfbn=3,xdgcfh=7,pxhkd=1,lc-,ksl-,jz-,rrv-,vpk=2,ktz=2,gtj-,glmz=7,rs-,tsv-,dxdxl-,xdfb-,gpz-,dkzsqb-,bnzq=2,tvd-,bknq-,zbb-,cmsf-,tpfrc=5,kqm=6,nscnp-,xlc=9,qxg=3,mz=9,zxm-,xlf=4,hkbf-,zxm=9,ksr=6,xm=6,kjzsmv=9,sk-,sxzhh-,cnq=5,hff-,mrq-,ps-,mdzc=5,tkxd-,jc-,pqgmnl=4,dsz=5,rqc=6,gnpz=3,vkk-,nsl-,sjn-,ljd-,tkxd=4,fbh-,htm-,dtjxv=8,lj-,xl-,dkp=5,vtm-,gltc=7,fps-,btjv-,kqm-,dsqgh=1,fgdk=5,svg=6,hq-,rf=6,vvl=6,jh=8,jrqk=5,pldx=4,llq-,tn=1,kkgj=9,bb=6,hpph-,xg=1,lkt=1,njfx=1,cdh=6,ps=2,sbd=7,qk=1,jf=3,vd=2,xgnr=7,knf=9,xj-,pb=2,pnm-,clt-,klpm=1,kgm=3,xpj=8,xb-,lrnz-,hrzhq-,crqn-,dc=1,rb-,jf=9,cf=3,pcv-,sctzlm=3,gb=5,mqg-,nckm-,xtp=2,cnq=5,vgk-,xb=8,kvjm=2,gb-,mdzc-,mtr=8,cntd-,fbn=7,sp-,kbvd-,qk-,fjg=2,xm=6,kgq-,krsqtb=2,ns-,nfk=8,qjh=2,qh=2,vl=9,jzrhx-,bfq-,gnpz=3,vrrqpg-,xmd-,frb=5,gj=3,jgts-,ssv=7,mdjn=2,xfb=5,gql=8,mql=8,tkl=3,rx=7,mdjn=1,sp-,ddf-,tnc-,kxb=6,gdlq-,gq-,xmd-,xs=8,xnfpt-,nsf-,fl-,rsnbql-,jhf-,nbd-,hdt-,rrl=5,krsqtb=5,lxzg-,vpk-,nbh=8,gq-,cr=9,msb=2,lsr-,ppd-,njfx=9,czj-,jgts-,mq-,rf=9,hdt-,mnmshn-,hqngsj=7,qnjfm-,fntc-,rqc-,mq-,fszj-,dh=2,sl=9,llphh=5,zt=6,mvgfg-,zct-,bgzmkc=7,cx-,mc-,dtjxv=3,cntd-,fntc=7,lrt-,ddf=7,mc-,lsl=7,nqqm-,hh-,vb=9,xlc=1,jtc=1,gtf-,tt=1,lrt=1,ljd=9,sp=2,rk-,lxzg=3,nscnp=9,tg=9,tnr=5,hkbf-,cgb-,mfr=5,rv=5,zr=4,sjn=3,gd-,fj-,kqm=8,xnfpt=9,xgnr-,mfg=1,qqz=9,fvjj=3,ckprt-,hl=2,gnpz-,mtkbz=3,hxd-,pmjz=7,lv-,kh-,stldk-,qbp-,hrrmd=6,vvd=6,mqh-,ckprt-,mdjn-,dhg-,qh=8,vtm=2,hsblgj=5,xdfb-,ptn=1,rk-,rv-,sh=2,kf=9,bqv-,bnzq=7,mks-,kt=1,fk-,cmsf-,dqclp=4,hq=2,hmbhhr=2,km=5,lbp=9,lmdg-,fsb-,jxp-,hmbhhr-,xm-,vd-,hpph=8,cbvr-,bf=1,ktz=2,vvd=8,jbf-,xl-,cqggjb-,dpp-,dpq-,tn-,vs=4,kdn=3,hrrmd-,klpm=3,hdvdp=6,hj-,df=2,vrrqpg-,fjg=9,ngfn=4,fgz=4,gpmx=9,hdvdp-,dhg=1,mdjn-,nbh=4,zfh-,qggxgx=4,fqj=1,sxv=9,nklnq-,hhhx=1,tnr-,czh-,hc=4,sqz=5,fp-,lrqrhk-,jhs-,rb=1,cbv=9,pldx=4,rrv-,srn=5,vm=1,gdm-,tgg=7,dzrg-,cr-,qm-,ll-,vkk-,vck-,pxhkd-,sxv-,ntbjb=9,cncd-,hd=8,sbd=7,nng=5,xdqd=2,tt=7,jm=8,htm=3,qvxv=6,cntd-,mdjn=4,mq=1,xrl-,rd=3,lrt=4,njfx=9,ql-,lrqrhk-,rbpq-,mzn=4,vs=8,vs=2,mtr-,pch-,mdzc=9,ctl-,lcv=1,srn-,tnkt-,px=5,jz-,qbp=3,jh=5,pc-,jh-,sjn=1,vtm=7,mf=6,xd=9,pnm=2,jpk-,llq=7,hv=6,dxtz=6,fjg=8,nfk-,fk=8,xh-,tnsmf-,cf=8,rx=9,jcj=5,dxtz-,fj=5,hff-,vfk=8,rvnklp-,kq=8,hrzhq-,gtf=5,llh-,qcrvs=3,phl=6,jfjgt=6,jzd=5,rv=6,kvtcc=5,ql-,dx=2,dpp=8,td=2,cncd=6,xhgt-,px-,jj=1,vvt-,jqhkf=1,hjq=2,mtkbz-,pzz=2,km=9,tkxd=6,xrl-,jhs-,pqgmnl=1,kvtcc-,vb-,jf-,xtp-,fhp=7,qtk-,nbh=5,sxzhh=9,vd=9,hq-,fjg=9,kjzsmv=8,bqv-,mrq-,pkdf-,nvc=3,fbn-,bdm=6,czh-,fvjj-,bq=1,jpk=7,lrt-,dj-,llq-,mljpt=8,dhg=9,pnfmgj-,rfbn=2,sbd-,fbhzcd=3,pdk-,rp=8,xgmft=9,sndpd=4,kjq=5,sxv=9,xlf-,xdpj=6,ptn=2,vtld=9,htm-,xdgcfh-,hg=9,dqclp-,xvx-,gltc-,sc=7,xfb-,spv=1,nsl=3,xsr-,jzbv=2,ckprt=7,fj-,bp-,mbd=3,knf=6,jzd=8,jjhd-,qkx-,qm=9,hjq-,xdgcfh=8,kbvd-,jp=4,qcrvs=1,ktz=4,cr-,kxmpd-,lcv=5,vfk=1,gd-,mf-,rrv-,tvd-,lbng-,zr=5,hmt-,hd-,crqn-,htm-,xz-,cntd=7,pbnr-,kjzsmv-,xd-,vm-,mqr-,qnjfm=2,qggxgx=7,gxkv=7,dxkx-,hx=2,sxzhh-,bnzq=2,jzbv-,hv-,zqsd=8,ssv=9,gg-,rk-,ss-,rxkp=9,mqg=8,mp=6,qvxv-,hrrmd=3,tdvm=8,njh-,ftdll-,qggxgx-,tp-,gdlq=8,cqggjb-,hc-,lsl-,mdjn=3,vs=3,lbp-,jh-,cf-,pg-,mzn-,zfl=2,xg-,tmfn-,zbrq=5,czj=2,dsg=2,zqsd=5,jm=1,kthg-,hdvdp=5,bxmz-,bm=8,czj-,hsm=6,rqc-,ktqgm=9,bhxnh-,zlqd-,hq=6,ksr=7,mbj=8,sl=6,lbng=4,xnl-,nklnq=5,mz=6,zxm-,lgx=6,qqc-,hb-,fnqkh-,hrrmd-,gn-,gxkv-,ljd=5,nf-,rvnklp=4,sxv=5,dx-,vs=5,xm-,mkvj=7,rr-,ts=8,sjn-,hsm=9,rhq-,xb-,mfnkh=3,hxd=5,pcp-,hprc=1,pl=1,cqggjb=2,qbms-,rhq=7,qbp=9,hnb=8,tn=6,hl-,pt-,ksr=4,cgb-,cbvr=1,dc-,fdf-,cnq=6,cx=9,tn=5,mbd=3,rfbn=4,ckprt-,kmcgcr=7,ps=7,hj-,fvjj-,ll-,mljpt=5,fnqkh=1,jzd-,pbnr=5,rr=6,qvd=2,mz-,rhq-,ml-,qk-,hgxd=7,bnzq-,qkl=4,ckr-,dr=4,klpm-,qzts=2,mnmshn=2,sndpd=1,mljpt=3,kmmvl=8,vkk=2,kgq-,crqn=6,jm-,njfx-,xb=2,bxmz=8,lmx-,cqggjb-,jz-,gpz-,vck=3,hj=1,xnfpt=8,zpfsz-,tkl=2,hm=4,ctl=6,pg=7,qvxv=6,kvjm-,cdh-,dhb-,cm=5,pnfmgj-,kvjm-,hxd-,nqqm-,cqggjb-,gnpz-,lbng-,kk-,vs-,dt-,kthg-,dzrg=1,jfjgt=5,ljd=4,qkkm=2,zb-,mq=6,bfq=8,bb=6,kxmpd-,pqgmnl-,cfj-,stldk-,dsg-,clt=2,sk=7,kq-,pt-,nbq-,vst=7,cf-,zbrq=2,nxxhk=4,xlf=2,tkpp=4,qkx=9,hj=1,sc=6,kgm=2,qggxgx=1,rcb-,kmcgcr=1,sh=7,xj=5,kbvd-,cp-,hqngsj-,rvnklp-,jz=8,mc-,xgmft-,mqh=9,nsl=9,jjhd=9,dsqgh-,kdn=4,vrrqpg-,mdzc=2,dj=9,ljd-,sc-,pb-,jqh=1,mbj-,rv=2,mtkbz=6,zfl=1,qk=9,mzn-,kdn=1,gg=7,jjfl-,zh=2,nscnp-,fvjj=4,qvd-,btjv-,gl=7,tnc-,xf=1,fp-,xlf=1,hqngsj=2,pqgmnl=8,jzbv-,zgj=1,xl=6,mqh-,qqz-,mdzc-,lkt=5,jp=5,kxb=8,dpp=5,qcrvs=1,fhp-,bq=8,vxzxc-,kf=3,nfk-,qqz-,bhxnh=1,sxv-,kbvd-,llq=2,pnfmgj=3,mfnkh=7,vd-,zgj=5,rxkp-,gtj-,ft=2,dhb=1,dm=5,ps-,ml-,rx=7,cntd-,lrz=7,xm-,knf=4,nbq=6,rsnbql-,kq-,zr=9,xsk-,mks=4,pmlhn-,xg-,nbh=1,dzrg-,njfx-,zf=4,jl=8,tkpp=2,jb-,km-,bd-,nn-,hprc=8,mfr-,xhblcq-,kt-,qvxv-,hgxd=2,ps-,kbvd=3,ftdll=1,lrt=7,mvgfg-,fx=2,xlf=6,bs-,xrlqx-,hpph=4,dpp-,lrt-,pm=2,jrqk-,qqc-,mtr=9,kgq-,sk-,kk-,tvd-,dqclp-,fjg=3,pg=1,hmt=5,sctzlm-,sp=4,rrv=9,jpk-,nc=7,dqclp=1,llq-,gmt-,dr=4,mks=3,tgsdsb-,mz-,cqggjb=6,vck-,dhb-,fgdk=1,mfnkh=1,hgxd=9,pch=6,lkt-,vmh=2,jclf-,ld=7,clt-,jh=5,jxp-,kjzsmv-,dtjxv-,jgp=2,fps=5,rt-,lkt=4,dh-,qcrvs-,pc-,mdjn=6,fptk=4,hv=7,sp=9,kdn-,cbn=1,sb-,fqj-,xsk-,nfk-,rr=1,qn=4,nch-,zfh-,xhblcq-,sndpd-,lc-,gn=1,pzz-,rrv-,xtp=4,ns=6,fntl-,gn-,mc=7,qk=9,mp=6,dxdxl=8,sndpd-,pl-,qggxgx=2,pcp-,srn-,zb-,hmbhhr=1,rb-,hprc-,kx=6,vm=9,pkdf=3,mc-,gn=7,bgzmkc=5,kxb=1,pxhkd-,kgm=3,fbn=2,xdpj=4,sb=1,td-,td-,tvd=9,lrz=2,pnm-,qkkm=6,ctz=6,dhb=5,cmsf=4,xtp=7,gn=9,bdm-,nklnq=5,kthg=3,sk-,tn=2,crqn-,zc-,kqm-,tn-,nsf-,qbp-,nsl=7,glnl-,kk-,dtjxv=9,mdjn=7,njh=4,ckr=4,hv-,hsm-,spv-,sctzlm-,rhq=8,mtr-,km=6,lj=6,zpfsz-,tlhm=4,dkp-,dl-,crqn-,pf-,dtr-,lv=4,pg=7,fgz=4,rr-,mz-,kcv=6,bknq-,hx-,rxkp-,rhq-,kk=4,rqc=8,cqggjb-,tt=4,mc=7,zs-,czh=6,xnl=7,dltvb-,lbng=8,kv=6,kxb=6,bh=7,ckr=7,bpf=9,pmjz-,kq-,czh-,vb=4,ds=6,bqv-,bfq-,zct=7,lkmt-,qx=4,hb-,zf=6,fsb=3,nc-,lmx=9,pcv-,ntbjb-,kf-,vm-,cnq=3,lbp-,hb-,dsqgh-,llh=4,ftdll=7,ns-,sn-,qzts=8,xf-,ts-,lbp-,nn=5,ksr-,xnfpt=6,glmz=5,vfk=9,jgts-,rkd=6,jqhkf=6,xsr=6,nbk=5,kp=7,mkvj=9,lnh-,zfxmb=9,xrlqx=9,gd=6,jf-,hqngsj-,vxzxc=7,kq=1,bp=4,gl=9,zbb=9,bd-,kt=4,jtc=9,hdt=2,ngfn-,xd-,zxm=1,lkmt=1,fj=1,kthg-,jzd=7,lnh-,jrqk-,mztj=3,kh=8,fntl-,fp-,dsz-,tkm-,fps=9,jb=8,vs-,vvd=3,bhxnh=9,rrl-,zr=4,mqr-,bq-,kgm=1,clt-,kthg-,srn=1,qjh-,kgq-,lkt-,vxzxc-,tdvm-,gdlq-,qbms=7,kdn-,gd=2,gdm-,hkbf=9,ltp=4,mfnkh=7,qh=4,vvd=6,ht-,hjq-,dsqgh-,pch=5,pcv=9,xz=2,sh=3,mql=4,xgnr=1,zct=7,qbms-,jj-,xfb-,qk=8,bh=9,hkbf=4,mztj-,sc-,nknrvm=6,zc=4,rr-,fps=7,dtr-,vgk-,rkd=4,cbv-,cv-,sgmp-,kxb=1,mks=4,tkl=1,pcp-,ntbjb-,cv=5,vkk=9,clt=9,ftdll=1,hpph=4,htm=9,nng-,fkdp-,xnfpt-,vs-,bqv=6,qqz-,vgk-,kxmpd=8,fl=7,bm-,hhhx=4,qzts=3,srn-,kv=3,cm-,tk-,dm=2,kfhn=6,mql=2,mdzc=5,pm=7,nbd-,brfm-,gj-,tvd=4,tmfn=5,dltvb-,gm=9,ln=1,nvp=2,mdjn=5,mfg=8,tnsmf=6,qkl=5,dc=8,bdm=9,kdn=9,nscnp=5,hb=8,zgj-,lmdg-,dhb=1,mkvj-,bfq=3,ljd=9,xhgt=6,jp-,zpn-,krsqtb=1,ztgx=1,htm=8,bfq=8,dqclp-,qkl=4,cdh-,pk=3,zxdj=3,gxkv-,kkgj-,xlc-,xhblcq-,hx=6,xhgt-,fntc-,fps=3,pcp=2,xj-,xhblcq-,hb-,fgz-,hdvdp-,gtj=7,qnjfm-,mfg=1,bp-,tn-,bqv=5,rd=4,pnfmgj-,dkzsqb=2,ds=2,jgts=7,pch=8,bb-,sdv-,tk=2,xtp-,jx=7,mf-,kx=8,dqclp=3,fntc-,dtr=1,nvp=5,vrg-,ngfn=1,pcv-,pl-,kqm=1,vl=2,btjv-,dl=9,dm=8,hv-,glmz-,ssv=2,njh=1,rfcgxr=8,gnpz-,lv=9,bpf=8,vl-,lbng=2,lkt-,tt=7,pqgmnl=1,rsnbql-,rd-,hxd-,dxtz-,ts-,hd=4,jbf-,cp=9,cm=1,bdx=7,cbn=9,dxdxl-,sjfx-,nsl-,rcb-,hprc-,rkd-,xfb=2,ltp-,nn=5,czh=1,kx=6,vst-,fk=3,sn=1,llh-,hprc=7,bqv=4,dtjxv-,tn-,nckm-,fps-,lsr-,hl=3,rcb-,hrzhq-,cfj=4,nbk-,gmt-,qh-,nknrvm=4,qn-,tt-,xvx=6,gxkv=4,dtr=5,fjg=1,mdjn-,vs=1,dsqgh-,rr=5,ml-,spv=4,ll=7,cntd=3,gd-,qxg=6,nxxhk=8,qvxv-,hprc-,xdpj-,xs=7,pqgmnl-,rbpq=9,mn=3,mql=4,fgdk=4,cdr=5,sn-,mljpt=5,pt-,zf-,lxzg-,gnpz=6,pc=6,zbb-,nscnp-,ptn-,mnmshn=1,xmd-,tczns=2,gpmx=5,nf-,pch=2,lj=1,frb-,crqn-,nbd=3,gtf-,lsl-,fgdk=8,xs-,hpph-,kthg-,dkzsqb-,qn-,cgb-,fnqkh-,vl-,kgq-,kbvd-,jrqk-,jclf-,cntd=9,ksl-,ctl-,klpm=8,rbpq-,jcj-,nlmj-,qxg-,jl=4,cbv-,nqqm=8,pmlhn=9,vl=8,bp=5,tczns-,spv=3,ksr=7,zb-,ztgx=5,bm=2,lrz-,lrz=3,vkk=4,nch=3,ngfn=9,nklnq=5,jtc=8,ht=9,nfk-,fnqkh-,xsk-,pkdf-,jxp-,jm-,sxzhh-,zr-,svg=8,kv=4,sxv-,kxmpd-,jgp=8,qdjff=3,fgdk=4,xnfpt-,lkbj=4,xd=2,jclf-,ht-,bq=3,gl=8,jtc-,pc=4,tnc-,sdnmh=4,kf=1,lgx-,dvc-,rg-,hff-,sqz-,zc-,hjq-,xdpj-,pt=9,xh-,llh-,mp=7,lbng-,mqg-,qdjff-,zfl=1,kkgj-,dhb=5,sdnmh-,jc=9,hsm-,rt=4,xf=1,xdpj=6,qcrvs-,hsm=7,mqg=7,hsblgj=9,sk-,ckr=9,xl-,kq-,cbvr-,cdh-,sndpd=2,sk-,qnjfm=5,jjfl-,tmfn=6,nknrvm=4,dsz-,zt=6,nc=2,btjv=5,bhxnh=9,ld=4,rr=1,kv=2,jxp-,tkl-,dr-,pldx=1,jh=2,gg-,jclf=5,kgm-,fgz-,rkd-,xlf-,vpk-,zt-,nckm-,sc-,vm-,jfjgt-,jp-,tpfrc=8,bknq-,dr-,jzv-,hjq-,kdn=6,fps-,tkm=8,glmz-,xlc=5,xz=2,bdz-,gpz=6,vck-,lv-,lxzg-,zpfsz=2,kcv=1,xdfb-,jcj-,bdx=1,pdk=8,qqz-,fk-,tgg-,hrzhq-,lrt=9,zh-,jhf-,kq-,llq=2,fgdk-,hbp=9,hrzhq-,ckprt=2,cncd-,fx-,pmjz-,pc-,hprc-,rbpq=6,jzrhx-,bknq-,nf-,jrqk=3,czj-,tt=8,hdt=9,rsnbql-,nng-,hpph-,cbbt-,tk=7,zpn=1,cb=5,zgt-,btjv-,ddf-,mfnkh=4,bp-,bd=9,zlqd=9,cx-,fgdk-,jb-,ld-,dvc-,kcv-,lkbj=8,mf-,hj=8,px=5,zpn-,qzts-,glmz-,rxkp-,rs=8,hsblgj-,bq-,ztgx=2,qvxv-,qbms-,nlmj-,sndpd-,sxzhh-,nbd=2,cnq=6,qm=5,dqclp=3,vrg=6,sjfx=8,rt-,hsblgj=5,tj=8,xg-,bknq=1,bdm-,zpn-,hd-,kjzsmv=1,fhp=9,hrrmd=1,ftdll-,xdpj-,dtr-,rbzlx-,nc-,fj-,rr=4,tg-,pmjz=1,kmmvl-,pnm-,glnl=9,rfbn-,hdt=6,sndpd=3,dkp=3,hbp-,sctzlm-,qn-,xdpj-,kq-,zpfsz-,vrrqpg=3,pt=3,qcf-,mfbj=3,hv-,jgp-,mfr=5,cmsf-,mql-,dsz-,dvc-,xdqd=9,lbng=9,fntl=1,kmmvl=3,hjq=4,rvnklp-,fptk=5,qqz=5,tkpp-,qm-,pmd=6,mfnkh=7,kp-,kvjm=9,xrl-,bd-,nch-,fp-,sn=8,zh-,vtm=2,bfq=4,zfh=7,sqz=9,km=9,qkx=1,xb=3,fgdk=2,cbn-,bq-,frb=2,fvjj-,vl-,zfxmb=7,dh=1,fntl-,ckprt-,gmt=9,px=3,mn=4,gl=6,rg=8,sc=4,ltp-,mvs-,kq-,tkxd-,tnr=9,nvp-,pch-,jhf=1,fkdp=7,rmdlr=4,qjh-,hdvdp=3,zf=1,pzz-,mvs=5,rcb-,vl=7,fptk=8,ckr=3,hb-,mc-,qbp=2,nvp-,pk=6,tlhm=4,rf-,ljd-,jzd=4,sxv-,pzz=6,pt=9,lsr-,tvd=2,dr=2,zs=4,sqz=1,dh=9,zb-,hdvdp=5,kvtcc=8,mfr=4,tnr-,nf=4,qc-,lkbj-,cb=2,qx=9,cgb=7,rk=9,zxdj-,mbd=9,bgzmkc=7,mr-,ksr=2,mvs=7,xgmft=6,dkp=1,nknrvm=3,hc=8,rf=8,fntl-,cp-,xhblcq=2,sndpd=1,xpj=2,xlf=4,pmjz=6,nxxhk=9,hdvdp-,gnpz=1,mvs=2,kthg=2,nbd=5,qvxv=7,pdk-,cmsf=7,jzbv=6,vkk=1,sp-,fdf-,dsqgh-,jhf=3,dhg=6,knf-,mfbj-,ctz-,jqh=1,nbq-,jzbv=8,mn=5,xfb=3,nklnq=8,cntd-,mfnkh-,pkdf-,mfbj-,ts=4,gdlq-,cfj=2,nfk=4,nhgz-,zb-,cdh-,nklnq-,fdf=6,kjzsmv-,cp-,fbhzcd-,rxkp-,rb-,lkmt-,czh=8,xlf=9,llq-,jxp=3,rcb-,fsb=9,cv=5,bs=6,hsm=1,cbbt=4,mtkbz=8,cmsf-,vl=1,kqkl-,xsr-,zlc-,qkx-,dsg=9,hsm-,hff-,dx=3,pkdf=4,zbb-,zbb-,pl-,flzg=3,rxkp=1,dsg-,hq-,bm=5,sndpd-,rfbn=4,jfjgt=8,tnc=1,qk-,gdlq=5,fx-,cbbt-,mp-,zfxmb=9,kkgj-,lc-,rb=5,cbv=1,dzrg-,nbq=6,qdh=5,bnzq=6,zs=5,czh=3,rk-,mn=7,hjq=3,pkdf=6,zr-,mz-,dkzsqb-,pmjz-,dxtz=9,hd-,lkmt=9,nbq-,zgj=9,fndhj-,ctl-,fx=3,xl=1,kbvd-,rxkp=1,ckr-,cbvr=1,bb=2,xfb-,rfbn=5,ksl=9,kq=6,kjq-,qqz-,xrlqx=3,dkzsqb-,rrv=6,vkk=2,cbv=7,mnmshn-,kkgj=4,mtr-,czh=5,tt-,vtld=1,mtr-,bknq=2,fjg=2,pmd=7,rd-,mfbj=8,jp=7,pnfmgj=8,fszj=5,tp-,fx=3,ppd-,zct=4,gj-,glnl-,kd=5,dpq-,xtp=7,ngfn=7,ljd=8,xs=6,jz-,lkbj-,bf=6,dtr=4,tn-,jclf=1,ld=4,sgmp=8,hnb=2,sjfx-,dvc=7,pbnr=6,xb-,pg-,rx-,rx-,pzz=8,zz-,vmcq=7,cx-,dpp=1,fp=7,dpp=1,srn-,zpfsz=3,nf=4,tkxd-,lnh=4,zz=4,vl=5,sdv-,bm-,qtk=1,ft-,pbnr-,qtk-,xvx=7,nckm-,bdx=2,bgzmkc-,mz-,zbb-,gdlq=6,vb-,zkknr=2,zfxmb-,bb=4,cntd-,pk-,hmt-,kxb-,pxhkd=5,ftdll-,qggxgx-,cntd=4,nvc=5,tmfn-,vb-,xf-,td=9,cncd=6,rp=4,xdpj-,cp-,tg=9,jp-,ksl-,jj-,bqv=7,nscnp=7,hc-,nsl=2,dpp-,xs-,ppd-,zfl-,qtk-,hxd=4,czh=1,mqh-,cbv=6,ctz-,llq-,hv=1,ld-,njkr-,jzv=5,tkm=7,xs-,dhg=9,mqg=3,rp=3,rvnklp-,ltp=5,jcj=9,qvd=7,dxtz=7,dh=8,kq-,vkk=4,tnr=9,ts-,mzn-,fj=6,jzrhx=9,pk=5,ptn-,nsf-,ssv=1,bdz-,rcb-,cr-,cgb-,jm-,fk-,btjv=4,kqm=3,ktz-,pb=5,fntl-,rxkp=7,phl=3,flzg-,zct-,vpk-,cm-,nhgz-,xrl=5,kx-,msb-,fntc-,mfg=2,fl-,nvp=3,jhf=3,nqqm=6,nf=7,cgb=6,qh=7,jzbv-,fsb=9,fnqkh-,fkdp-,tj=4,hbp-,fbhzcd-,bxmz-,lkmt=2,rs-,nbq-,xtp-,xrlqx=9,ksr=2,mqr-,tsv=4,qqz=7,sxzhh=4,zz=5,tvd=1,mnmshn-,xrl=5,gm=7,jtc-,pl-,nc-,rfcgxr=4,msb-,tg-,hb-,zpfsz-,rmdlr=4,pzz-,pb=5,qm=7,ns-,xdgcfh-,cf=2,pf-,ps=3,mtr=7,hj=3,hv=9,qjh=9,jb=4,hj-,mvgfg=5,xmd=5,lsl-,xhgt-,zpn-,qzgb=3,lrqrhk=3,zpfsz=3,vfk=4,xpt-,gn-,sl-,xm=4,ctz=7,hb-,gpz-,kvjm=2,rb-,jhs=4,ds=3,rk-,sbd-,rp=1,kr=4,ftdll=1,bh=7,zxm-,nsl=7,xgnr=8,sh=6,vtm-,cnq-,ddf=6,nfk=2,qkx-,hq=3,tnr-,nqqm-,xd=8,cntd=1,zgt-,nsl-,mz-,ql-,svg=8,cncd=9,ntbjb=6,lj=3,hdg-,mc=3,vm=4,fntc=9,tt-,km=1,fj=8,fbhzcd=7,tt=4,fk-,pt-,kf=1,kh=8,kdn-,mfnkh-,jjhd-,btjv-,kgm-,tnr-,zb-,zf=4,hv-,ts-,cdr-,rv-,vs-,frb-,zct-,gpz-,htm=3,mn-,nbh-,dxtz=2,zpfsz-,ksl=4,cm-,dsqgh=1,pzz-,lrqrhk-,bd-,jh-,cmsf-,smf-,jl=2,bh=4,mfbj=1,mzn-,dsqgh=2,hx=5,lrnz-,vtm-,ckr-,lkt-,ssv-,klpm=4,kt-,bnzq=2,dtr=8,hsm=7,kf-,tdvm-,hjq-,dvc-,pcv-,vvt-,bp-,jcj=3,sndpd-,xfb=9,vd=7,gb-,sb=6,nsf=4,cbvr=7,phl=5,fl=7,fsb-,mtkbz=5,sbd=4,vs-,tkm=7,rx-,zlc=9,xnl-,jb=3,rp-,qcrvs=8,zb=7,xnl=5,lrt=4,rv=9,mqg-,rmdlr-,ckprt-,mqh-,nhgz=3,spv-,xm=8,nlmj=7,fvjj-,lsl-,xb-,vtld-,gn-,gd-,cv-,pg-,fgdr=9,sgmp=5,xj=4,zt-,fgz=3,krsqtb=8,jhs-,kd=9,fvjj=4,ptn=3,sgmp=7,ckprt-,hnb=1,mfg-,qqc-,mfg=1,gpmx=8,kmmvl=7,nc-,sxzhh-,mq=9,hpph=5,xgnr=6,tpfrc=9,zf-,qk=6,hj=3,czh=5,cnq=4,frb=3,ptn=8,sjn=3,tj=4,rkd-,sjn=7,hv=5,gd-,lkmt=1,ppd-,tkm=4,sxv-,ln=7,hprc-,cr=8,xdqd-,sbd=9,sxzhh-,pc-,zqsd=3,cdr=3,nknrvm=4,sc=8,fgdk=7,mz=6,hhhx-,hq-,qbms=3,cbvr-,mks-,hprc=1,nch-,fntc-,vgk-,ld-,rfcgxr-,nf=1,hdt-,rbpq=6,jhs=9,kvjm=3,pdk=8,mqr-,dtr=9,jjhd=1,gpz=9,qk-,nch=6,njh=4,ft=8,mnmshn=7,tgsdsb-,xrl=1,sctzlm-,tj-,llphh-,cdr-,tkl=2,vl-,ht=1,bpf=2,bpf-,gnpz-,tsv-,ps-,mp-,kthg-,tgsdsb=4,bqv-,rp=7,dpp=9,rkd-,gmt-,qc-,zlc=7,gl-,ts=1,vrrqpg=1,jm-,mks-,jfjgt=3,kcv-,msb=6,nbq-,qvd=7,gxkv-,dsg-,zlqd-,qzts-,dpp-,xhblcq=3,hsblgj=8,vmcq=3,cv-,mfbj=5,fndhj=1,dv-,ctz=5,bknq-,sp=2,rhq-,fx-,lcv-,cnq-,hv=9,jzrhx-,hh=9,bm-,jclf-,dm-,sndpd=6,bh=2,fbh=6,dm=3,frb-,zpn-,cdr=5,jzbv=8,dh-,qdh=6,lcv-,ngfn-,dzrg=3,xgnr=7,kjq=4,sqz-,fp=7,tn-,bdm=1,hgxd=7,srn-,fjg-,rsnbql-,sgmp-,xfb-,jxp-,bf=9,fkdp=6,tkm-,jj-,kk-,tmfn=3,mzn=4,xdqd-,rvnklp=4,nsl=4,jfjgt=9,qk=6,bdm=7,ddf=5,bhxnh-,rfbn=9,ln-,xj-,xb-,lbng-,xs-,ftdll-,lbng=5,gltc-,lnh-,kthg=5,fps=3,km=4,jpk=6,ft=8,bpf=6,nn-,ds=1,mn=5,jqh-,xlf=6,mfnkh=6,dc=9,tp=3,bnzq=1,xsk=7,lrqrhk-,gn-,mqh=5,bf=1,fl=5,crqn=9,dm=6,cfj-,kcv=6,zlqd=5,fkdp=8,sgmp-,rqc=8,bdz=5,qjv-,vd=2,gxkv=3,bdm=1,msb=1,fnqkh=7,df=9,rd=3,xhgt-,ts-,stldk=9,gn=3,dv=5,dvc=6,lkbj=2,mfbj-,hl-,rvnklp-,sbd=8,klq-,qdjff=5,lc=6,pch=7,hm=2,gdlq-,kjzsmv-,dsz=3,gd=4,vfk-,jbf=5,srn=9,stldk=5,kqkl-,bf=1,dhb-,ngfn-,ftdll=1,xd-,hprc=2,nng-,rcb=3,dsg=9,cbbt=3,fkdp-,bq=7,tkpp-,dtjxv-,hxd=1,rg-,rs=7,gg-,jbf=8,nvc-,jxxhbv-,dxtz-,fkdp-,gjtf-,ll-,xz-,bfq=6,rt=7,mfnkh=1,hdt-,cv-,hbp=7,hxd=1,gl=6,nsl-,ftdll=7,zbb-,vb-,zqsd=1,qjv=6,ng-,nvc-,zs-,zfh-,lnh=3,hxd-,lbng-,ctz-,knf-,jzd-,mqg-,gxkv-,pc=1,hc-,qxg-,dtr-,dkp-,hl=5,xdqd-,rk-,mtr-,qjv=9,lv-,vpk=7,hjq-,mvgfg=4,sqz=8,ng-,jxxhbv=7,zxm=1,kjzsmv-");
        }


        [Fact]
        public void can_parse_lookbehind()
        {
            data.Extract<string>(@"(?<=(12))");
        }

        [Fact]
        public void can_extract_to_tuple()
        {
            var (a, b, c, d, e, f, g, h, i) = data.Extract<(int, char, string, int, char, string, int, char, string)>(pattern);

            Assert.IsType<int>(a);
            Assert.IsType<char>(b);
            Assert.IsType<string>(c);
            Assert.IsType<int>(d);
            Assert.IsType<char>(e);
            Assert.IsType<string>(f);
            Assert.IsType<int>(g);
            Assert.IsType<char>(h);
            Assert.IsType<string>(i);

            Assert.Equal(1, a);
            Assert.Equal('2', b);
            Assert.Equal("3", c);
            Assert.Equal(4, d);
            Assert.Equal('5', e);
            Assert.Equal("6", f);
            Assert.Equal(7, g);
            Assert.Equal('8', h);
            Assert.Equal("9", i);
        }

        record PositionalRecord(int a, char b, string c, int d, char e, string f, int g, char h, string i);

        [Fact]
        public void can_extract_to_positional_record()
        {
            PositionalRecord? record = data.Extract<PositionalRecord>(pattern);

            Assert.NotNull(record);

            var (a, b, c, d, e, f, g, h, i) = record!;

            Assert.IsType<int>(a);
            Assert.IsType<char>(b);
            Assert.IsType<string>(c);
            Assert.IsType<int>(d);
            Assert.IsType<char>(e);
            Assert.IsType<string>(f);
            Assert.IsType<int>(g);
            Assert.IsType<char>(h);
            Assert.IsType<string>(i);

            Assert.Equal(1, a);
            Assert.Equal('2', b);
            Assert.Equal("3", c);
            Assert.Equal(4, d);
            Assert.Equal('5', e);
            Assert.Equal("6", f);
            Assert.Equal(7, g);
            Assert.Equal('8', h);
            Assert.Equal("9", i);
        }

        [Fact]
        public void fails_when_positional_record_is_wrong_arity()
        {
            Assert.Throws<ArgumentException>(() => data.Extract<PositionalRecord>(pattern_nested));
        }

        record PropertiesRecord
        {
            public string? s { get; init; }
            public long? n { get; init; }
            public int? a { get; init; }
            public char? b { get; init; }
            public string? c { get; init; }
            public int? d { get; init; }
            public char? e { get; init; }
            public string? f { get; init; }
            public int? g { get; init; }
            public char? h { get; init; }
            public string? i { get; init; }
        }

        // Don't currently handle nested named captures, and I'm not sure we ever will.
        //[Fact]
        //public void can_extract_named_capture_groups_to_properties()
        //{
        //    PropertiesRecord? record = data.Extract<PropertiesRecord>(pattern_named);
        //}

        record Passport
        {
            public int? byr { get; set; }
            public int? iyr { get; set; }
            public int? eyr { get; set; }
            public string? hgt { get; set; }
            public string? hcl { get; set; }
            public string? ecl { get; set; }
            public string? pid { get; set; }
        }

        [Fact]
        public void can_extract_mondo_conditional_regex()
        {
            var mondoString = @"
^(?:\b
(?: (?:byr: (?:(?<byr>19[2-9][0-9]|200[0-2])                           |.*?) )
|    (?:iyr: (?:(?<iyr>20(?:1[0-9]|20))                                   |.*?) )
|    (?:eyr: (?:(?<eyr>20(?:2[0-9]|30))                                   |.*?) )
|    (?:hgt: (?:(?<hgt>(?:(?:59|6[0-9]|7[0-6])in)|(?:1(?:[5-8][0-9]|9[0-3])cm)) |.*?) )
|    (?:hcl: (?:(?<hcl>\#[0-9a-f]{6})                                   |.*?) )
|    (?:ecl: (?:(?<ecl>amb|blu|brn|gry|grn|hzl|oth)                     |.*?) )
|    (?:pid: (?:(?<pid>[0-9]{9})                                        |.*?) )
|    (?:cid: (?:.*?)                                                          )
)
\b\s*)+
$
";
            var mondo = new Regex(mondoString, RegexOptions.IgnorePatternWhitespace);

            var result = "hgt:61in iyr:2014 pid:916315544 hcl:#733820 ecl:oth".Extract<Passport>(mondoString,RegexOptions.IgnorePatternWhitespace);

            Assert.Equal(new Passport { hgt = "61in", iyr = 2014, pid = "916315544", hcl = "#733820", ecl = "oth" }, result);

            //TODO: this was the only test using the `this Match` extension for Extract. Should re-add one.
        }

        record Container
        {
            public string? container { get; init; }
            public List<int?>? count { get; init; }
            public List<string?>? bag { get; init; }
            public string? none { get; init; }
        }

        [Fact]
        public void can_extract_capture_collections_to_lists()
        {
            var line = "faded yellow bags contain 4 mirrored fuchsia bags, 4 dotted indigo bags, 3 faded orange bags, 5 plaid crimson bags.";
            var regex = @"^(?<container>.+) bags contain(?: (?<none>no more bags\.)| (?<count>\d+) (?<bag>[^,.]*) bag[s]?[,.])+$";

            var output = line.Extract<Container>(regex);

            Assert.Equivalent(new Container
            {
                container = "faded yellow",
                count = new List<int?> { 4, 4, 3, 5 },
                bag = new List<string?> { "mirrored fuchsia", "dotted indigo", "faded orange", "plaid crimson" },
                none = null
            }, output);
        }

        [Fact]
        public void can_extract_single_item()
        {
            var output = "asdf".Extract<string>("(.*)");
            Assert.Equal("asdf", output);

            var n = "2023".Extract<int>(@"(\d+)");
            Assert.Equal(2023, n);
        }

        [Fact]
        public void can_extract_multimatch_to_list()
        {
            var result = "123 456 789".Extract<List<int>> (@"(?:(\d+) ?)+");

            Assert.Equal([123, 456, 789], result);
        }

        [Fact]
        public void can_extract_multimatch_to_hashset()
        {
            var result = "123 456 789".Extract<HashSet<int>>(@"(?:(\d+) ?)+");

            Assert.Equal(new HashSet<int> { 123, 456, 789 }, result);
        }

        [Fact]
        public void can_extract_alternation_to_tuple()
        {
            var result = "asdf".Extract<(int?, string)>(@"(\d+)|(.*)");

            Assert.Equal((null, "asdf"), result);
            
            result = "123".Extract<(int?, string)>(@"(\d+)|(.*)");

            Assert.Equal((123, null), result);
        }

        record Alternation(int? n, string s);

        record NamedAlternation
        {
            public int? n { get; init; }
            public string? s { get; init; }
        }

        [Fact]
        public void can_extract_alternation_to_record()
        {
            var result = "asdf".Extract<Alternation>(@"(\d+)|(.*)");

            Assert.Equal(new Alternation(null, "asdf"), result);

            var result_named = "asdf".Extract<NamedAlternation>(@"(?<n>\d+)|(?<s>.*)");

            Assert.Equal(new NamedAlternation { n = null, s = "asdf" }, result_named);
        }

        [Fact]
        public void can_extract_enum()
        {
            var result = "Asynchronous,Encrypted".Extract<System.IO.FileOptions>(@".*");

            Assert.Equal(System.IO.FileOptions.Asynchronous | System.IO.FileOptions.Encrypted, result);
        }

        record WithTemplate(string op, int arg)
        {
            public const string REGEXTRACT_REGEX_PATTERN = @"(\S+) ([+-]?\d+)";
            public const RegexOptions REGEXTRACT_REGEX_OPTIONS = RegexOptions.None;
        }

        [Fact]
        public void can_extract_with_template()
        {
            var result = "acc +7".Extract<WithTemplate>();

            Assert.Equal(new WithTemplate("acc", 7), result);
        }

        const RegexOptions opts = RegexOptions.IgnoreCase|RegexOptions.Multiline;

        [Fact]
        public void can_extract_to_string_constructor()
        {
            var result = "https://www.google.com/ 12345".Extract<(Uri,int)>(@"(.*) (\d+)");
            Assert.Equal((new Uri("https://www.google.com/"), 12345), result);
        }

        [Fact]
        public void regex_does_not_match()
        {
            Assert.Throws<ArgumentException>(()=>"https://www.google.com/".Extract<Uri>(@"\d+"));
        }

        record bounds(int lo, int hi);

        [Fact]
        public void nested_extraction()
        {
            // TODO: Why is this so much slower than nested_extraction_control?
            var result = "2-12 c: abcdefg".Extract<(bounds, char, string)>(@"((\d+)-(\d+)) (.): (.*)");

            Assert.Equal((new bounds(2, 12), 'c', "abcdefg"), result);
        }

        [Fact]
        public void nested_extraction_control()
        {
            var result = "2-12 c: abcdefg".Extract<((int lo, int hi), char ch, string str)>(@"((\d+)-(\d+)) (.): (.*)");

            Assert.Equal(((2, 12), 'c', "abcdefg"), result);
        }


        [Fact]
        public void nested_extraction_of_list()
        {
            // TODO: Why does this need the outer parens here: "((\w)+)"?
            var plan = ExtractionPlan<List<List<char>>>.CreatePlan(new Regex(@"((\w)+ ?)+"));
            var str = plan.ToString("x");
            output.WriteLine(str);
            
            var result = plan.Extract("The quick brown fox jumps over the lazy dog.");

            Assert.Equal([['T', 'h', 'e'], ['q', 'u', 'i', 'c', 'k'], ['b', 'r', 'o', 'w', 'n'], ['f', 'o', 'x'], ['j', 'u', 'm', 'p', 's'], ['o', 'v', 'e', 'r'], ['t', 'h', 'e'], ['l', 'a', 'z', 'y'], ['d', 'o', 'g']], result);
        }

        [Fact]
        public void harder_nested_extraction_of_list()
        {
            // TODO: This currently doesn't quite work without extra parens outsize the top level.
            var plan = ExtractionPlan<List<List<List<char>>>>.CreatePlan(new Regex(@"(((\w)+ ?)+,? ?)+"));
            var str = plan.ToString("x");
            output.WriteLine(str);

            var result = plan.Extract("asdf lkj, wero oiu");

            Assert.Equal([[['a', 's', 'd', 'f'], ['l', 'k', 'j']], [['w', 'e', 'r', 'o'], ['o', 'i', 'u']]], result);
        }

        [Fact]
        public void nested_extraction_of_bags()
        {
            var line = "faded yellow bags contain 4 mirrored fuchsia bags, 4 dotted indigo bags, 3 faded orange bags, 5 plaid crimson bags.";
            var regex = @"^(.+) bags contain(?: (no more bags\.)| ((\d+) ([^,.]*)) bag[s]?[,.])+$";

            var output = line.Extract<(string,string,List<(int,string)>)>(regex);

            Assert.Equivalent(("faded yellow", default(string), new List<(int, string)> { (4, "mirrored fuchsia"), (4, "dotted indigo"), (3, "faded orange"), (5, "plaid crimson") }), output);
        }

        [Fact]
        public void extraction_plan()
        {
            var regex = new Regex(@"((\d+)-(\d+)) (.): (.*)");
            var match = regex.Match("2-12 c: abcdefg");
            var plan = ExtractionPlan<((int, int), char, string)>.CreatePlan(regex);
            var result = plan.Extract(match);

            output.WriteLine(plan.ToString("x"));

            Assert.Equal(((2, 12), 'c', "abcdefg"), result);
        }

        [Fact]
        public void extraction_plan_to_long_tuple()
        {
            var regex = new Regex(pattern);
            var match = Regex.Match(data, pattern);
            var plan = ExtractionPlan<(int?, char, string, int, char, string, int, char, string)>.CreatePlan(regex);

            var (a, b, c, d, e, f, g, h, i) = plan.Extract(match);

            Assert.IsType<int>(a);
            Assert.IsType<char>(b);
            Assert.IsType<string>(c);
            Assert.IsType<int>(d);
            Assert.IsType<char>(e);
            Assert.IsType<string>(f);
            Assert.IsType<int>(g);
            Assert.IsType<char>(h);
            Assert.IsType<string>(i);

                 var (verb, year) =
              "Party like it's 1999"
               .Extract<(string, int)>
            (@"(\w+) like it's (\d)+");

            Assert.Equal(1, a);
            Assert.Equal('2', b);
            Assert.Equal("3", c);
            Assert.Equal(4, d);
            Assert.Equal('5', e);
            Assert.Equal("6", f);
            Assert.Equal(7, g);
            Assert.Equal('8', h);
            Assert.Equal("9", i);
        }
        
        [Fact]
        public void debug()
        {
            //var data = "faded yellow bags contain 4 mirrored fuchsia bags, 4 dotted indigo bags, 3 faded orange bags, 5 plaid crimson bags.";
            //var plan = RegexExtractionPlan.CreatePlan<(string, string, List<(int?, string)?>)>(@"^(.+) bags contain(?: (no other bags)\.| ((\d+) (.*?)) bags?[,.])+$");
            //var result = plan.Execute(Regex.Match(data, @"^(.+) bags contain(?: (no other bags)\.| ((\d+) (.*?)) bags?[,.])+$"));

            Regex rx;
            var plan = CreateAndLogPlan<bagdescription>(/* language=regex */@"^(?<name>.+) bags contain(?: (?<none>no other bags)\.| (?<contents>(\d+) (.*?)) bags?[,.])+$");
            var result = plan.Extract("faded yellow bags contain 4 mirrored fuchsia bags, 4 dotted indigo bags, 3 faded orange bags, 5 plaid crimson bags.");

            Assert.Equivalent(new bagdescription { name = "faded yellow", contents = new List<includedbags> { new includedbags(4, "mirrored fuchsia"), new includedbags(4, "dotted indigo"), new includedbags(3, "faded orange"), new includedbags(5, "plaid crimson") } }, result);
        }

        record bagdescription
        {
            public string? name { get; init; }
            public string? none { get; init; }
            public List<includedbags>? contents { get; init; }
        }
        record includedbags(int? num, string name);

        [Fact]
        public void CreateTreePlan()
        {
            var regex = new Regex(@"((\d+)-(\d+)) (.): (.*)");
            var plan = ExtractionPlan<((int?, int?)?, char, string)?>.CreatePlan(regex);
            object? result = plan.Extract(regex.Match("2-12 c: abcdefgji"));

            regex = new Regex(@"((\w)+ ?)+");
            var plan2 = ExtractionPlan<List<List<char>>>.CreatePlan(regex);

            result = plan2.Extract(regex.Match("The quick brown fox jumps over the lazy dog"));
        }

        [Fact]
        public void deep_tuple_type_tree()
        {
            var plan = CreateAndLogPlan<(int, (int, int), int, int, int, int, (int, (List<int>, int)), int, int, int, int, int, int, int, int)>(@"(\d+) \(((\d+) (\d+))\) (\d+) (\d+) (\d+) (\d+) ((\d+) \(((\d+ ?)+ (\d+))\)) (\d+) (\d+) (\d+) (\d+) (\d+) (\d+) (\d+) (\d+)");

            var result = plan.Extract("1 (2 3) 4 5 6 7 8 (9 10 11 12 13) 14 15 16 17 18 19 20 21");

            Assert.Equivalent((1, (2, 3), 4, 5, 6, 7, (8, (new List<int> { 9, 10, 11, 12 }, 13)), 14, 15, 16, 17, 18, 19, 20, 21), result);
        }

        [Fact]
        public void can_create_polymorphic_parse_plan()
        {
            var plan = CreateAndLogPlan<instr>(@"(.*)");
            var results = plan.Extract("mask = lkjasdf");
            Assert.Equal(new maskinstr("lkjasdf"), results);
        }

        record instr()
        {
            public static instr Parse(string str)
            {
                if (str.StartsWith("mask"))
                {
                    return str.Extract<maskinstr>(@"mask = (.+)")!;
                }
                else
                {
                    return str.Extract<meminstr>(@"mem\[(\d+)] = (\d+)")!;
                }
            }
        }
        record maskinstr(string mask) : instr;
        record meminstr(long loc, long val) : instr;

        private ExtractionPlan<T> CreateAndLogPlan<T>(string v)
        {
            var plan = ExtractionPlan<T>.CreatePlan(new Regex(v));
            output.WriteLine(plan.ToString("x"));

            return plan;
        }
    }
}

// This is here to enable use of record types in .NET 3.1.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
