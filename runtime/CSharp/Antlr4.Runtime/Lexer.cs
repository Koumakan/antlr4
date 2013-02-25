/*
 * [The "BSD license"]
 *  Copyright (c) 2013 Terence Parr
 *  Copyright (c) 2013 Sam Harwell
 *  All rights reserved.
 *
 *  Redistribution and use in source and binary forms, with or without
 *  modification, are permitted provided that the following conditions
 *  are met:
 *
 *  1. Redistributions of source code must retain the above copyright
 *     notice, this list of conditions and the following disclaimer.
 *  2. Redistributions in binary form must reproduce the above copyright
 *     notice, this list of conditions and the following disclaimer in the
 *     documentation and/or other materials provided with the distribution.
 *  3. The name of the author may not be used to endorse or promote products
 *     derived from this software without specific prior written permission.
 *
 *  THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 *  IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 *  OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 *  IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
 *  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 *  NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 *  DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 *  THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 *  (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 *  THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using Sharpen;

namespace Antlr4.Runtime
{
    /// <summary>A lexer is recognizer that draws input symbols from a character stream.</summary>
    /// <remarks>
    /// A lexer is recognizer that draws input symbols from a character stream.
    /// lexer grammars result in a subclass of this object. A Lexer object
    /// uses simplified match() and error recovery mechanisms in the interest
    /// of speed.
    /// </remarks>
    public abstract class Lexer : Recognizer<int, LexerATNSimulator>, ITokenSource
    {
        public const int DefaultMode = 0;

        public const int DefaultTokenChannel = TokenConstants.DefaultChannel;

        public const int Hidden = TokenConstants.HiddenChannel;

        public const int MinCharValue = '\u0000';

        public const int MaxCharValue = '\uFFFE';

        public ICharStream _input;

        protected internal Tuple<ITokenSource, ICharStream> _tokenFactorySourcePair;

        /// <summary>How to create token objects</summary>
        protected internal ITokenFactory _factory = CommonTokenFactory.Default;

        /// <summary>The goal of all lexer rules/methods is to create a token object.</summary>
        /// <remarks>
        /// The goal of all lexer rules/methods is to create a token object.
        /// This is an instance variable as multiple rules may collaborate to
        /// create a single token.  nextToken will return this object after
        /// matching lexer rule(s).  If you subclass to allow multiple token
        /// emissions, then set this to the last token to be matched or
        /// something nonnull so that the auto token emit mechanism will not
        /// emit another token.
        /// </remarks>
        public IToken _token;

        /// <summary>
        /// What character index in the stream did the current token start at?
        /// Needed, for example, to get the text for current token.
        /// </summary>
        /// <remarks>
        /// What character index in the stream did the current token start at?
        /// Needed, for example, to get the text for current token.  Set at
        /// the start of nextToken.
        /// </remarks>
        public int _tokenStartCharIndex = -1;

        /// <summary>The line on which the first character of the token resides</summary>
        public int _tokenStartLine;

        /// <summary>The character position of first character within the line</summary>
        public int _tokenStartCharPositionInLine;

        /// <summary>Once we see EOF on char stream, next token will be EOF.</summary>
        /// <remarks>
        /// Once we see EOF on char stream, next token will be EOF.
        /// If you have DONE : EOF ; then you see DONE EOF.
        /// </remarks>
        public bool _hitEOF;

        /// <summary>The channel number for the current token</summary>
        public int _channel;

        /// <summary>The token type for the current token</summary>
        public int _type;

        public readonly List<int> _modeStack = new List<int>();

        public int _mode = Antlr4.Runtime.Lexer.DefaultMode;

        /// <summary>
        /// You can set the text for the current token to override what is in
        /// the input char buffer.
        /// </summary>
        /// <remarks>
        /// You can set the text for the current token to override what is in
        /// the input char buffer.  Use setText() or can set this instance var.
        /// </remarks>
        public string _text;

        public Lexer(ICharStream input)
        {
            this._input = input;
            this._tokenFactorySourcePair = Tuple.Create((ITokenSource)this, input);
        }

        public virtual void Reset()
        {
            // wack Lexer state variables
            if (_input != null)
            {
                _input.Seek(0);
            }
            // rewind the input
            _token = null;
            _type = TokenConstants.InvalidType;
            _channel = TokenConstants.DefaultChannel;
            _tokenStartCharIndex = -1;
            _tokenStartCharPositionInLine = -1;
            _tokenStartLine = -1;
            _text = null;
            _hitEOF = false;
            _mode = Antlr4.Runtime.Lexer.DefaultMode;
            _modeStack.Clear();
            Interpreter.Reset();
        }

        /// <summary>
        /// Return a token from this source; i.e., match a token on the char
        /// stream.
        /// </summary>
        /// <remarks>
        /// Return a token from this source; i.e., match a token on the char
        /// stream.
        /// </remarks>
        public virtual IToken NextToken()
        {
            if (_input == null)
            {
                throw new InvalidOperationException("nextToken requires a non-null input stream."
                    );
            }
            // Mark start location in char stream so unbuffered streams are
            // guaranteed at least have text of current token
            int tokenStartMarker = _input.Mark();
            try
            {
                while (true)
                {
                    if (_hitEOF)
                    {
                        EmitEOF();
                        return _token;
                    }
                    _token = null;
                    _channel = TokenConstants.DefaultChannel;
                    _tokenStartCharIndex = _input.Index;
                    _tokenStartCharPositionInLine = Interpreter.GetCharPositionInLine();
                    _tokenStartLine = Interpreter.GetLine();
                    _text = null;
                    do
                    {
                        _type = TokenConstants.InvalidType;
                        //				System.out.println("nextToken line "+tokenStartLine+" at "+((char)input.LA(1))+
                        //								   " in mode "+mode+
                        //								   " at index "+input.index());
                        int ttype;
                        try
                        {
                            ttype = Interpreter.Match(_input, _mode);
                        }
                        catch (LexerNoViableAltException e)
                        {
                            NotifyListeners(e);
                            // report error
                            Recover(e);
                            ttype = TokenTypes.Skip;
                        }
                        if (_input.La(1) == IntStreamConstants.Eof)
                        {
                            _hitEOF = true;
                        }
                        if (_type == TokenConstants.InvalidType)
                        {
                            _type = ttype;
                        }
                        if (_type == TokenTypes.Skip)
                        {
                            goto outer_continue;
                        }
                    }
                    while (_type == TokenTypes.More);
                    if (_token == null)
                    {
                        Emit();
                    }
                    return _token;
outer_continue: ;
                }
            }
            finally
            {
                // make sure we release marker after match or
                // unbuffered char stream will keep buffering
                _input.Release(tokenStartMarker);
            }
        }

        /// <summary>
        /// Instruct the lexer to skip creating a token for current lexer rule
        /// and look for another token.
        /// </summary>
        /// <remarks>
        /// Instruct the lexer to skip creating a token for current lexer rule
        /// and look for another token.  nextToken() knows to keep looking when
        /// a lexer rule finishes with token set to SKIP_TOKEN.  Recall that
        /// if token==null at end of any token rule, it creates one for you
        /// and emits it.
        /// </remarks>
        public virtual void Skip()
        {
            _type = TokenTypes.Skip;
        }

        public virtual void More()
        {
            _type = TokenTypes.More;
        }

        public virtual void Mode(int m)
        {
            _mode = m;
        }

        public virtual void PushMode(int m)
        {
            _modeStack.Add(_mode);
            Mode(m);
        }

        public virtual int PopMode()
        {
            if (_modeStack.Count == 0)
            {
                throw new InvalidOperationException();
            }

            int mode = _modeStack[_modeStack.Count - 1];
            _modeStack.RemoveAt(_modeStack.Count - 1);
            Mode(mode);
            return _mode;
        }

        public virtual ITokenFactory TokenFactory
        {
            get
            {
                return _factory;
            }
            set
            {
                ITokenFactory factory = value;
                this._factory = factory;
            }
        }

        /// <summary>Set the char stream and reset the lexer</summary>
        public virtual void SetInputStream(ICharStream input)
        {
            this._input = null;
            this._tokenFactorySourcePair = Tuple.Create((ITokenSource)this, _input);
            Reset();
            this._input = input;
            this._tokenFactorySourcePair = Tuple.Create((ITokenSource)this, _input);
        }

        public virtual string SourceName
        {
            get
            {
                return _input.SourceName;
            }
        }

        public override IIntStream InputStream
        {
            get
            {
                return _input;
            }
        }

        ICharStream ITokenSource.InputStream
        {
            get
            {
                return (ICharStream)InputStream;
            }
        }

        /// <summary>
        /// By default does not support multiple emits per nextToken invocation
        /// for efficiency reasons.
        /// </summary>
        /// <remarks>
        /// By default does not support multiple emits per nextToken invocation
        /// for efficiency reasons.  Subclass and override this method, nextToken,
        /// and getToken (to push tokens into a list and pull from that list
        /// rather than a single variable as this implementation does).
        /// </remarks>
        public virtual void Emit(IToken token)
        {
            //System.err.println("emit "+token);
            this._token = token;
        }

        /// <summary>
        /// The standard method called to automatically emit a token at the
        /// outermost lexical rule.
        /// </summary>
        /// <remarks>
        /// The standard method called to automatically emit a token at the
        /// outermost lexical rule.  The token object should point into the
        /// char buffer start..stop.  If there is a text override in 'text',
        /// use that to set the token's text.  Override this method to emit
        /// custom Token objects or provide a new factory.
        /// </remarks>
        public virtual IToken Emit()
        {
            IToken t = _factory.Create(_tokenFactorySourcePair, _type, _text, _channel, _tokenStartCharIndex
                , CharIndex - 1, _tokenStartLine, _tokenStartCharPositionInLine);
            Emit(t);
            return t;
        }

        public virtual IToken EmitEOF()
        {
            int cpos = Column;
            // The character position for EOF is one beyond the position of
            // the previous token's last character
            if (_token != null)
            {
                int n = _token.StopIndex - _token.StartIndex + 1;
                cpos = _token.Column + n;
            }
            IToken eof = _factory.Create(_tokenFactorySourcePair, TokenConstants.Eof, null, TokenConstants
                .DefaultChannel, _input.Index, _input.Index - 1, Line, cpos);
            Emit(eof);
            return eof;
        }

        public virtual int Line
        {
            get
            {
                return Interpreter.GetLine();
            }
            set
            {
                int line = value;
                Interpreter.SetLine(line);
            }
        }

        public virtual int Column
        {
            get
            {
                return Interpreter.GetCharPositionInLine();
            }
            set
            {
                int charPositionInLine = value;
                Interpreter.SetCharPositionInLine(charPositionInLine);
            }
        }

        /// <summary>What is the index of the current character of lookahead?</summary>
        public virtual int CharIndex
        {
            get
            {
                return _input.Index;
            }
        }

        /// <summary>
        /// Return the text matched so far for the current token or any text
        /// override.
        /// </summary>
        /// <remarks>
        /// Return the text matched so far for the current token or any text
        /// override.
        /// </remarks>
        /// <summary>
        /// Set the complete text of this token; it wipes any previous changes to the
        /// text.
        /// </summary>
        /// <remarks>
        /// Set the complete text of this token; it wipes any previous changes to the
        /// text.
        /// </remarks>
        public virtual string Text
        {
            get
            {
                if (_text != null)
                {
                    return _text;
                }
                return Interpreter.GetText(_input);
            }
            set
            {
                string text = value;
                this._text = text;
            }
        }

        /// <summary>Override if emitting multiple tokens.</summary>
        /// <remarks>Override if emitting multiple tokens.</remarks>
        public virtual IToken Token
        {
            get
            {
                return _token;
            }
            set
            {
                IToken _token = value;
                this._token = _token;
            }
        }

        public virtual int Type
        {
            get
            {
                return _type;
            }
            set
            {
                int ttype = value;
                _type = ttype;
            }
        }

        public virtual int Channel
        {
            get
            {
                return _channel;
            }
            set
            {
                int channel = value;
                _channel = channel;
            }
        }

        public virtual string[] ModeNames
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Used to print out token names like ID during debugging and
        /// error reporting.
        /// </summary>
        /// <remarks>
        /// Used to print out token names like ID during debugging and
        /// error reporting.  The generated parsers implement a method
        /// that overrides this to point to their String[] tokenNames.
        /// </remarks>
        public override string[] TokenNames
        {
            get
            {
                return null;
            }
        }

        /// <summary>Return a list of all Token objects in input char stream.</summary>
        /// <remarks>
        /// Return a list of all Token objects in input char stream.
        /// Forces load of all tokens. Does not include EOF token.
        /// </remarks>
        public virtual IList<IToken> GetAllTokens()
        {
            IList<IToken> tokens = new List<IToken>();
            IToken t = NextToken();
            while (t.Type != TokenConstants.Eof)
            {
                tokens.Add(t);
                t = NextToken();
            }
            return tokens;
        }

        public virtual void Recover(LexerNoViableAltException e)
        {
            if (_input.La(1) != IntStreamConstants.Eof)
            {
                // skip a char and try again
                Interpreter.Consume(_input);
            }
        }

        public virtual void NotifyListeners(LexerNoViableAltException e)
        {
            string text = _input.GetText(Interval.Of(_tokenStartCharIndex, _input.Index));
            string msg = "token recognition error at: '" + GetErrorDisplay(text) + "'";
            IAntlrErrorListener<int> listener = GetErrorListenerDispatch();
            listener.SyntaxError(this, 0, _tokenStartLine, _tokenStartCharPositionInLine, 
                msg, e);
        }

        public virtual string GetErrorDisplay(string s)
        {
            StringBuilder buf = new StringBuilder();
            foreach (char c in s.ToCharArray())
            {
                buf.Append(GetErrorDisplay(c));
            }
            return buf.ToString();
        }

        public virtual string GetErrorDisplay(int c)
        {
            string s = ((char)c).ToString();
            switch (c)
            {
                case TokenConstants.Eof:
                {
                    s = "<EOF>";
                    break;
                }

                case '\n':
                {
                    s = "\\n";
                    break;
                }

                case '\t':
                {
                    s = "\\t";
                    break;
                }

                case '\r':
                {
                    s = "\\r";
                    break;
                }
            }
            return s;
        }

        public virtual string GetCharErrorDisplay(int c)
        {
            string s = GetErrorDisplay(c);
            return "'" + s + "'";
        }

        /// <summary>
        /// Lexers can normally match any char in it's vocabulary after matching
        /// a token, so do the easy thing and just kill a character and hope
        /// it all works out.
        /// </summary>
        /// <remarks>
        /// Lexers can normally match any char in it's vocabulary after matching
        /// a token, so do the easy thing and just kill a character and hope
        /// it all works out.  You can instead use the rule invocation stack
        /// to do sophisticated error recovery if you are in a fragment rule.
        /// </remarks>
        public virtual void Recover(RecognitionException re)
        {
            //System.out.println("consuming char "+(char)input.LA(1)+" during recovery");
            //re.printStackTrace();
            // TODO: Do we lose character or line position information?
            _input.Consume();
        }
    }
}
