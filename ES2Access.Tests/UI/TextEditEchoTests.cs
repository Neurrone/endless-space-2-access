using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Tests.Speech;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The whole of what a live text edit says, decided by comparing the box with itself a frame ago.
    /// Nothing else in the mod can be tested for it: the characters arrive from the engine's own
    /// accumulated-input string, which no injection can fake.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class TextEditEchoTests
    {
        private static string Space
        {
            get { return ModStrings.Get(ModStrings.EditCaretSpace); }
        }

        private static string Blank
        {
            get { return ModStrings.Get(ModStrings.EditCaretBlank); }
        }

        [Fact]
        public void TypingACharacterEchoesIt()
        {
            EditEcho echo = TextEditEcho.Since("Dus", 3, "Dusa", 4);
            Assert.Equal(EditEchoKind.Typed, echo.Kind);
            Assert.Equal("a", echo.Text);
        }

        [Fact]
        public void TypingIntoTheMiddleEchoesTheCharacterNotTheTail()
        {
            EditEcho echo = TextEditEcho.Since("Dsay", 1, "Dusay", 2);
            Assert.Equal(EditEchoKind.Typed, echo.Kind);
            Assert.Equal("u", echo.Text);
        }

        [Fact]
        public void ATypedSpaceIsNamed()
        {
            EditEcho echo = TextEditEcho.Since("New", 3, "New ", 4);
            Assert.Equal(EditEchoKind.Typed, echo.Kind);
            Assert.Equal(Space, echo.Text);
        }

        [Fact]
        public void BackspaceEchoesTheCharacterItRemoved()
        {
            EditEcho echo = TextEditEcho.Since("Dusay", 5, "Dusa", 4);
            Assert.Equal(EditEchoKind.Deleted, echo.Kind);
            Assert.Equal("y", echo.Text);
        }

        [Fact]
        public void ForwardDeleteEchoesTheCharacterUnderTheCaret()
        {
            // The caret does not move: the character to its right goes.
            EditEcho echo = TextEditEcho.Since("Dusay", 2, "Duay", 2);
            Assert.Equal(EditEchoKind.Deleted, echo.Kind);
            Assert.Equal("s", echo.Text);
        }

        [Fact]
        public void DeletingAWordEchoesTheWord()
        {
            EditEcho echo = TextEditEcho.Since("New Dusay", 9, "New ", 4);
            Assert.Equal(EditEchoKind.Deleted, echo.Kind);
            Assert.Equal("Dusay", echo.Text);
        }

        /// <summary>
        /// The game empties a box for reasons of its own - a chat line sent, a quantity clamped and
        /// rewritten - and reading the vanished text back as if the player had deleted it would say a
        /// sentence they never asked about. No keystroke can empty a box of several characters, so the
        /// silence costs nothing that a keystroke could have caused.
        /// </summary>
        [Fact]
        public void TheGameEmptyingTheBoxSaysNothing()
        {
            Assert.Equal(EditEchoKind.None, TextEditEcho.Since("hello", 5, "", 0).Kind);
        }

        [Fact]
        public void ClearingTheLastCharacterIsStillADeletion()
        {
            EditEcho echo = TextEditEcho.Since("a", 1, "", 0);
            Assert.Equal(EditEchoKind.Deleted, echo.Kind);
            Assert.Equal("a", echo.Text);
        }

        [Fact]
        public void AWholesaleRewriteSaysNothing()
        {
            Assert.Equal(EditEchoKind.None, TextEditEcho.Since("12", 2, "99", 2).Kind);
        }

        [Fact]
        public void ARejectedCharacterSaysNothing()
        {
            // The field filtered what was typed: nothing changed, and nothing is claimed to have.
            Assert.Equal(EditEchoKind.None, TextEditEcho.Since("12", 2, "12", 2).Kind);
        }

        [Fact]
        public void MovingLeftSpeaksTheCharacterSteppedOnto()
        {
            EditEcho echo = TextEditEcho.Since("Dusay", 5, "Dusay", 4);
            Assert.Equal(EditEchoKind.Caret, echo.Kind);
            Assert.Equal("y", echo.Text);
        }

        [Fact]
        public void HomeSpeaksTheFirstCharacter()
        {
            EditEcho echo = TextEditEcho.Since("Dusay", 5, "Dusay", 0);
            Assert.Equal(EditEchoKind.Caret, echo.Kind);
            Assert.Equal("D", echo.Text);
        }

        [Fact]
        public void EndOfTextSpeaksBlank()
        {
            EditEcho echo = TextEditEcho.Since("Dusay", 0, "Dusay", 5);
            Assert.Equal(EditEchoKind.Caret, echo.Kind);
            Assert.Equal(Blank, echo.Text);
        }

        [Fact]
        public void ACaretOverASpaceIsNamed()
        {
            EditEcho echo = TextEditEcho.Since("New Dusay", 0, "New Dusay", 3);
            Assert.Equal(EditEchoKind.Caret, echo.Kind);
            Assert.Equal(Space, echo.Text);
        }

        /// <summary>The engine parks the caret past the end until it has computed one, and the mod
        /// reads it raw - so an uncomputed caret must read as end-of-text rather than as a move.
        /// </summary>
        [Fact]
        public void ACaretPastTheEndIsEndOfText()
        {
            Assert.Equal(Blank, TextEditEcho.CharacterAt("Dusay", int.MaxValue));
            Assert.Equal(EditEchoKind.None, TextEditEcho.Since("Dusay", int.MaxValue, "Dusay", 5).Kind);
        }

        [Fact]
        public void AnEmptyBoxIsBlankWhereverTheCaretIs()
        {
            Assert.Equal(Blank, TextEditEcho.CharacterAt("", 0));
            Assert.Equal(Blank, TextEditEcho.CharacterAt(null, 0));
        }

        [Fact]
        public void NothingHappeningSaysNothing()
        {
            Assert.Equal(EditEchoKind.None, TextEditEcho.Since(null, 0, null, 0).Kind);
            Assert.Equal(EditEchoKind.None, TextEditEcho.Since("Dusay", 2, "Dusay", 2).Kind);
        }
    }
}
