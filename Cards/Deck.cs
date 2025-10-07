using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A deck of "cards" (split into a library, a hand, and a graveyard).
/// The actual type of the cards is generic.
/// </summary>
public class Deck<T> {
    public enum Zone {
        LIBRARY,
        HAND,
        GRAVEYARD
    }

    private List<T> deckList;
    private Stack<T> library;
    private List<T> hand;
    private Stack<T> graveyard;

    public int handSize;

    public Deck(IEnumerable<T> deckList, int handSize = 5) {
        this.deckList = new List<T>();
        foreach (T card in deckList) this.deckList.Add(card);

        this.handSize = handSize;
    }

    /// <summary>
    /// Add the card to the deck.
    /// The card is placed at the given index of the given zone.
    /// </summary>
    /// <param name="card">The card to add.</param>
    /// <param name="addToDeckList">
    ///     <c>true</c> to add the card to the deck list. The card will be kept the next time the deck is reset.
    ///     <c>false</c> to not add the card to the deck list. The card will be dropped the next time the deck is reset.
    ///     Defaults to true.
    /// </param>
    /// <param name="zone">The zone to add the card to. Defaults to the library.</param>
    /// <param name="index">
    /// The index of the zone at which to add the card. Defaults to 0.
    /// Positive indices will be taken relative to the bottom of the zone.
    ///     '0' places the card on the bottom of the library or graveyard, or the left of the hand.
    /// Negative indices will be taken relative to the top of the zone.
    ///     '-1' places the card on the top of the library or graveyard, or the right of the hand.
    /// Positions where |pos| > zone.size will be clamped, so (assuming a 52-card deck):
    ///     '99' would place the card on the top of the library or graveyard, or the right of the hand, and
    ///     '-99' would place the card on the bottom of the library or graveyard, or the left of the hand.
    /// </param>
    public void Add(T card, bool addToDeckList = true, Deck.Zone zone = Deck.Zone.LIBRARY, int index = 0) {
        if (addToDeckList) this.deckList.Add(card);

        List<T> zoneCards;
        switch (zone) {
            case (Deck.Zone.LIBRARY):
                zoneCards = this.library.ToList();      // NOTE: copy
                break;
            case (Deck.Zone.HAND):
                zoneCards = this.hand;                  // NOTE: reference
                break;
            case (Deck.Zone.GRAVEYARD):
                zoneCards = this.graveyard.ToList();    // NOTE: copy
                break;
            default:
                break;
        }

        if (index >= 0) {
            index = min(index, zoneCards.Count);
        } else {
            index = max(index, -1 * zoneCards.Count);
            index = zoneCards.Count + index + 1;  // '+ 1' accounts for the fact the element will be inserted *before* the index
        }

        zoneCards.Insert(index, card);

        switch (zone) {
            case (Deck.Zone.LIBRARY):
                this.library = zoneCards.ToStack();
            // case (Deck.Zone.HAND): is not needed, since the hand could be used as reference and not as a copy
            case (Deck.Zone.GRAVEYARD):
                this.graveyard.ToStack();
            default:
                break;
        }
    }

    /// <summary>
    /// Discard the given card to the graveyard, if possible (ie the card is in the hand).
    /// </summary>
    /// <param name="card">The card to discard. <c>null</c> to discard a random card.</param>
    /// <returns><c>true</c> if the card could be discarded, else <c>false</c></returns>
    public bool Discard(T? card) {
        if (card is T c) return this.hand.Remove(c);
        else if (this.hand.Count != 0) {
            Random random = new Random();
            this.hand.RemoveAt(random.Next(this.hand.Count));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Discard cards until the hand is empty.
    /// </summary>
    public void DiscardHand() {
        while (this.hand.Count != 0) this.hand.RemoveAt(0);
    }

    /// <summary>
    /// Draw n cards from the library (adding them to the hand).
    /// </summary>
    /// <param name="n">The number of cards to draw.</param>
    public void Draw(int n = 1) {
        while (n-- > 0) {
            if (this.deckList.Count == 0) this.Reset(includeHand: false);
            this.hand.Add(this.library.Pop());
        }
    }

    /// <summary>
    /// Draw cards from the library (adding them to the hand) until the hand is full.
    /// </summary>
    public void DrawHand() {
        while (this.hand.Count < this.maxHandSize) this.Draw();
    }

    /// <summary>
    /// Play a card, if possible (ie the player has enough resources).
    /// Perform its effect, then discard it.
    /// </summary>
    /// <param name="card">The card to play.</param>
    /// <returns><c>true</c> if the card could be played, else <c>false</c></returns>
    public bool PlayCard(T card) {
        if (card.Cost > this.Mana) return false;
        this.Mana -= card.Cost;
        card.Play();
        this.Discard(card);
        return true;
    }

    /// <summary>
    /// Remove (the first instance of) a card from the deck.
    /// </summary>
    /// <param name="card">The card to remove.</param>
    /// <param name="removeFromDeckList">
    ///     <c>true</c> to remove the card from the deck list. The card will remain missing the next time the deck is reset.
    ///     <c>false</c> to not remove the card from the deck list. The card will be re-added the next time the deck is reset.
    ///     Defaults to true.
    /// </param>
    /// <param name="zone">
    ///     The zone to remove the card from.
    ///     If null, attempt to remove from each zone (first the hand, then the graveyard, then the library).
    ///     Defaults to null.
    /// </param>
    /// <returns><c>true</c> if the card could be removed (ie it existed in the given zone), else <c>false</c></returns>
    public bool Remove(T card, bool removeFromDeckList, Deck.Zone? zone = null) {
        if (removeFromDeckList) this.deckList.Remove(card);

        // check the hand first because it is the easiest
        if ((zone == null || zone == Deck.Zone.HAND)
            && this.hand.Remove(card)) return true;

        // then check the graveyard
        if (zone == null || zone == Deck.Zone.GRAVEYARD) {
            List graveyardList = this.graveyard.ToList();
            if (graveyardList.Remove(card)) {
                this.graveyard = graveyardList.ToStack();
                return true;
            }

        // then check the library
        if (zone == null || zone == Deck.Zone.LIBRARY) {
            List libraryList = this.library.ToList();
            if (libraryList.Remove(card)) {
                this.library = libraryList.ToStack();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Shuffle the library.
    /// </summary>
    public void ShuffleLibrary() {
        GameController.Shuffle(ref this.library);
    }

    /// <summary>
    /// Shuffle the graveyard.
    /// </summary>
    public void ShuffleGraveyard() {
        GameController.Shuffle(ref this.graveyard);
    }

    /// <summary>
    /// Move all cards from the hand and graveyard to the library,
    /// then shuffle the library.
    /// </summary>
    /// <param name="includeHand"><c>true</c> to scoop the hand as well as the graveyard, <c>false</c> to only scoop the graveyard.</param>
    private void Reset(bool includeHand = true) {
        if (includeHand) this.DiscardHand();
        this.library = this.graveyard;
        this.graveyard = new Stack<T>();
    }

    /// <summary>
    /// Shuffle the given stack (pile).
    /// </summary>
    /// <param name="stack">The stack to be shuffled.</param>
    public static void Shuffle<T>(ref Stack<T> stack) {
        if (stack == null) throw new ArgumentNullException(nameof(stack));

        List<T> list = new List<T>(stack.Count);
        while (stack.Count > 0) list.Add(stack.Pop());

        // Fisher-Yates algorithm
        Random rng = new Random();
        int n = list.Count;
        while (n > 1) {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }

        for (int i = list.Count - 1; i >= 0; i--) stack.Push(list[i]);
    }
}
