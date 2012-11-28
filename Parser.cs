
using System;

namespace Tastier {



public class Parser {
	public const int _EOF = 0;
	public const int _ident = 1;
	public const int _number = 2;
	public const int _string = 3;
	public const int maxT = 34;

	const bool T = true;
	const bool x = false;
	const int minErrDist = 2;
	
	public Scanner scanner;
	public Errors  errors;

	public Token t;    // last recognized token
	public Token la;   // lookahead token
	int errDist = minErrDist;

const int // types
      undef = 0, integer = 1, boolean = 2,str = 3;

   const int // object kinds
      var = 0, proc = 1;
  const int 
      mutable = 0, immutable = 1;

   public SymbolTable   tab;
   public CodeGenerator gen;
  
/*--------------------------------------------------------------------------*/


	public Parser(Scanner scanner) {
		this.scanner = scanner;
		errors = new Errors();
	}

	void SynErr (int n) {
		if (errDist >= minErrDist) errors.SynErr(la.line, la.col, n);
		errDist = 0;
	}

	public void SemErr (string msg) {
		if (errDist >= minErrDist) errors.SemErr(t.line, t.col, msg);
		errDist = 0;
	}
	
	void Get () {
		for (;;) {
			t = la;
			la = scanner.Scan();
			if (la.kind <= maxT) { ++errDist; break; }

			la = t;
		}
	}
	
	void Expect (int n) {
		if (la.kind==n) Get(); else { SynErr(n); }
	}
	
	bool StartOf (int s) {
		return set[s, la.kind];
	}
	
	void ExpectWeak (int n, int follow) {
		if (la.kind == n) Get();
		else {
			SynErr(n);
			while (!StartOf(follow)) Get();
		}
	}


	bool WeakSeparator(int n, int syFol, int repFol) {
		int kind = la.kind;
		if (kind == n) {Get(); return true;}
		else if (StartOf(repFol)) {return false;}
		else {
			SynErr(n);
			while (!(set[syFol, kind] || set[repFol, kind] || set[0, kind])) {
				Get();
				kind = la.kind;
			}
			return StartOf(syFol);
		}
	}

	
	void AddOp(out Op op) {
		op = Op.ADD; 
		if (la.kind == 4) {
			Get();
		} else if (la.kind == 5) {
			Get();
			op = Op.SUB; 
		} else SynErr(35);
	}

	void ConstVarDecl() {
		string name; int type; Obj obj; 
		Expect(6);
		Ident(out name);
		obj = tab.NewConstVar(name);  
		Expect(7);
		Expr(out type);
		tab.assignType(name,type); 
		if (obj.level == 0) gen.Emit(Op.STOG, obj.adr);
		else gen.Emit(Op.STO, tab.curLevel-obj.level, obj.adr); 
		Expect(8);
	}

	void Ident(out string name) {
		Expect(1);
		name = t.val; 
	}

	void Expr(out int type) {
		int type1; Op op; 
		SimExpr(out type);
		if (StartOf(1)) {
			RelOp(out op);
			SimExpr(out type1);
			if (type != type1) SemErr("incompatible types");
			gen.Emit(op); type = boolean; 
		}
	}

	void SimExpr(out int type) {
		int type1; Op op; 
		Term(out type);
		while (la.kind == 4 || la.kind == 5) {
			AddOp(out op);
			Term(out type1);
			if (type != integer || type1 != integer) 
			  SemErr("integer type expected");
			gen.Emit(op); 
		}
	}

	void RelOp(out Op op) {
		op = Op.EQU; 
		switch (la.kind) {
		case 18: {
			Get();
			break;
		}
		case 19: {
			Get();
			op = Op.LSS; 
			break;
		}
		case 20: {
			Get();
			op = Op.GTR; 
			break;
		}
		case 21: {
			Get();
			op = Op.NEQ; 
			break;
		}
		case 22: {
			Get();
			op = Op.LSE; 
			break;
		}
		case 23: {
			Get();
			op = Op.GTE; 
			break;
		}
		default: SynErr(36); break;
		}
	}

	void Factor(out int type) {
		int n; Obj obj; string name; string s; 
		type = undef; 
		switch (la.kind) {
		case 1: {
			Ident(out name);
			obj = tab.Find(name); type = obj.type;
			if (obj.kind == var) {
			  if (obj.level == 0) gen.Emit(Op.LOADG, obj.adr);
			//                                else gen.Emit(Op.LOAD, obj.adr);                            ***
			     else gen.Emit(Op.LOAD, tab.curLevel-obj.level, obj.adr); // 
			} else SemErr("variable expected"); 
			break;
		}
		case 2: {
			Get();
			n = Convert.ToInt32(t.val); 
			gen.Emit(Op.CONST, n); type = integer; 
			break;
		}
		case 5: {
			Get();
			Factor(out type);
			if (type != integer) {
			  SemErr("integer type expected"); type = integer;
			}
			gen.Emit(Op.NEG); 
			break;
		}
		case 9: {
			Get();
			gen.Emit(Op.CONST, 1); type = boolean; 
			break;
		}
		case 10: {
			Get();
			gen.Emit(Op.CONST, 0); type = boolean; 
			break;
		}
		case 3: {
			Get();
			s = t.val; gen.EmitStr(s); type = str; 
			break;
		}
		default: SynErr(37); break;
		}
	}

	void MulOp(out Op op) {
		op = Op.MUL; 
		if (la.kind == 11) {
			Get();
		} else if (la.kind == 12) {
			Get();
			op = Op.DIV; 
		} else SynErr(38);
	}

	void ProcDecl() {
		string name; Obj obj; int adr, adr2; 
		Expect(13);
		Ident(out name);
		obj = tab.NewObj(name, proc, undef); obj.adr = gen.pc;
		//                          if (name == "Main") gen.progStart = gen.pc;        
		if (name == "Main") {                           // 
		  obj.level= 0; gen.progStart = gen.pc;        // 
		}                                               // 
		  else obj.level = tab.curLevel+1;             // 
		tab.OpenScope(name); 
		Expect(14);
		Expect(15);
		Expect(16);
		gen.Emit(Op.ENTER, 0); adr = gen.pc - 2; 
		while (StartOf(2)) {
			if (la.kind == 6) {
				ConstVarDecl();
			} else if (la.kind == 31 || la.kind == 32 || la.kind == 33) {
				VarDecl();
			} else if (StartOf(3)) {
				Stat();
			} else {
				gen.Emit(Op.JMP, 0); adr2 = gen.pc - 2; 
				ProcDecl();
				gen.Patch(adr2, gen.pc); 
			}
		}
		Expect(17);
		gen.Emit(Op.LEAVE); gen.Emit(Op.RET);
		gen.Patch(adr, tab.topScope.nextAdr);
		tab.CloseScope(); 
	}

	void VarDecl() {
		string name; int type; 
		Type(out type);
		Ident(out name);
		tab.NewObj(name, var, type); 
		while (la.kind == 29) {
			Get();
			Ident(out name);
			tab.NewObj(name, var, type); 
		}
		Expect(8);
	}

	void Stat() {
		int type; string name; Obj obj;
		int adr, adr2, loopstart; 
		switch (la.kind) {
		case 1: {
			Ident(out name);
			obj = tab.Find(name); 
			if (la.kind == 7) {
				Get();
				if (obj.kind != var) SemErr("cannot assign to procedure");	//AN
				if (obj.mutability == immutable) SemErr("cannot reassign constant variable"); 
				Expr(out type);
				Expect(8);
				if (type != obj.type) SemErr("incompatible types");
				if (obj.level == 0) gen.Emit(Op.STOG, obj.adr);
				  else gen.Emit(Op.STO, tab.curLevel-obj.level, obj.adr); 
			} else if (la.kind == 14) {
				Get();
				Expect(15);
				Expect(8);
				if (obj.kind != proc) SemErr("object is not a procedure");
				  gen.Emit(Op.CALL, obj.level-tab.curLevel, obj.adr); 
			} else SynErr(39);
			break;
		}
		case 24: {
			Get();
			Expect(14);
			Expr(out type);
			Expect(15);
			if (type != boolean) SemErr("boolean type expected");
			  gen.Emit(Op.FJMP, 0); adr = gen.pc - 2; 
			Stat();
			if (la.kind == 25) {
				Get();
				gen.Emit(Op.JMP, 0); adr2 = gen.pc - 2;
				gen.Patch(adr, gen.pc); adr = adr2; 
				Stat();
			}
			gen.Patch(adr, gen.pc); 
			break;
		}
		case 26: {
			Get();
			loopstart = gen.pc; 
			Expect(14);
			Expr(out type);
			Expect(15);
			if (type != boolean) SemErr("boolean type expected");
			  gen.Emit(Op.FJMP, 0); adr = gen.pc - 2; 
			Stat();
			gen.Emit(Op.JMP, loopstart); gen.Patch(adr, gen.pc); 
			break;
		}
		case 27: {
			Get();
			Ident(out name);
			Expect(8);
			obj = tab.Find(name);
			if (obj.type != integer) SemErr("integer type expected");
			  gen.Emit(Op.READ);
			if (obj.level == 0) gen.Emit(Op.STOG, obj.adr);
			//                             else gen.Emit(Op.STO, obj.adr); .)                            ***
			  else gen.Emit(Op.STO, tab.curLevel-obj.level, obj.adr); 
			break;
		}
		case 28: {
			Get();
			Expr(out type);
			if(type == str) gen.Emit(Op.SWRITE);					//AN
			    else if(type == integer) gen.Emit(Op.WRITE);		//AN
			else SemErr("expected type int or string"); 
			while (la.kind == 29) {
				Get();
				Expr(out type);
				if(type == str) gen.Emit(Op.SWRITE); 					//AN
				     else if(type == integer) gen.Emit(Op.WRITE);		//AN
				else SemErr("expected type int or string"); 
			}
			Expect(8);
			gen.Emit(Op.NEWLINE);
			break;
		}
		case 16: {
			Get();
			while (StartOf(4)) {
				if (StartOf(3)) {
					Stat();
				} else if (la.kind == 31 || la.kind == 32 || la.kind == 33) {
					VarDecl();
				} else {
					ConstVarDecl();
				}
			}
			Expect(17);
			break;
		}
		default: SynErr(40); break;
		}
	}

	void Term(out int type) {
		int type1; Op op; 
		Factor(out type);
		while (la.kind == 11 || la.kind == 12) {
			MulOp(out op);
			Factor(out type1);
			if (type != integer || type1 != integer) 
			  SemErr("integer type expected");
			gen.Emit(op); 
		}
	}

	void Tastier() {
		string name; 
		Expect(30);
		Ident(out name);
		tab.OpenScope(name); 
		Expect(16);
		while (StartOf(5)) {
			if (la.kind == 6) {
				ConstVarDecl();
			} else if (la.kind == 31 || la.kind == 32 || la.kind == 33) {
				VarDecl();
			} else {
				ProcDecl();
			}
		}
		Expect(17);
		tab.CloseScope();
		if (gen.progStart == -1) SemErr("main function never defined");
		
	}

	void Type(out int type) {
		type = undef; 
		if (la.kind == 31) {
			Get();
			type = integer; 
		} else if (la.kind == 32) {
			Get();
			type = boolean; 
		} else if (la.kind == 33) {
			Get();
			type = str; 
		} else SynErr(41);
	}



	public void Parse() {
		la = new Token();
		la.val = "";		
		Get();
		Tastier();
		Expect(0);

	}
	
	static readonly bool[,] set = {
		{T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, T,T,T,T, x,x,x,x, x,x,x,x, x,x,x,x},
		{x,T,x,x, x,x,T,x, x,x,x,x, x,T,x,x, T,x,x,x, x,x,x,x, T,x,T,T, T,x,x,T, T,T,x,x},
		{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, T,x,T,T, T,x,x,x, x,x,x,x},
		{x,T,x,x, x,x,T,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, T,x,T,T, T,x,x,T, T,T,x,x},
		{x,x,x,x, x,x,T,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,T,x,x}

	};
} // end Parser


public class Errors {
	public int count = 0;                                    // number of errors detected
	public System.IO.TextWriter errorStream = Console.Out;   // error messages go to this stream
	public string errMsgFormat = "-- line {0} col {1}: {2}"; // 0=line, 1=column, 2=text

	public virtual void SynErr (int line, int col, int n) {
		string s;
		switch (n) {
			case 0: s = "EOF expected"; break;
			case 1: s = "ident expected"; break;
			case 2: s = "number expected"; break;
			case 3: s = "string expected"; break;
			case 4: s = "\"+\" expected"; break;
			case 5: s = "\"-\" expected"; break;
			case 6: s = "\"const\" expected"; break;
			case 7: s = "\":=\" expected"; break;
			case 8: s = "\";\" expected"; break;
			case 9: s = "\"true\" expected"; break;
			case 10: s = "\"false\" expected"; break;
			case 11: s = "\"*\" expected"; break;
			case 12: s = "\"/\" expected"; break;
			case 13: s = "\"void\" expected"; break;
			case 14: s = "\"(\" expected"; break;
			case 15: s = "\")\" expected"; break;
			case 16: s = "\"{\" expected"; break;
			case 17: s = "\"}\" expected"; break;
			case 18: s = "\"=\" expected"; break;
			case 19: s = "\"<\" expected"; break;
			case 20: s = "\">\" expected"; break;
			case 21: s = "\"!=\" expected"; break;
			case 22: s = "\"<=\" expected"; break;
			case 23: s = "\">=\" expected"; break;
			case 24: s = "\"if\" expected"; break;
			case 25: s = "\"else\" expected"; break;
			case 26: s = "\"while\" expected"; break;
			case 27: s = "\"read\" expected"; break;
			case 28: s = "\"write\" expected"; break;
			case 29: s = "\",\" expected"; break;
			case 30: s = "\"program\" expected"; break;
			case 31: s = "\"int\" expected"; break;
			case 32: s = "\"bool\" expected"; break;
			case 33: s = "\"string\" expected"; break;
			case 34: s = "??? expected"; break;
			case 35: s = "invalid AddOp"; break;
			case 36: s = "invalid RelOp"; break;
			case 37: s = "invalid Factor"; break;
			case 38: s = "invalid MulOp"; break;
			case 39: s = "invalid Stat"; break;
			case 40: s = "invalid Stat"; break;
			case 41: s = "invalid Type"; break;

			default: s = "error " + n; break;
		}
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}

	public virtual void SemErr (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}
	
	public virtual void SemErr (string s) {
		errorStream.WriteLine(s);
		count++;
	}
	
	public virtual void Warning (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
	}
	
	public virtual void Warning(string s) {
		errorStream.WriteLine(s);
	}
} // Errors


public class FatalError: Exception {
	public FatalError(string m): base(m) {}
}
}