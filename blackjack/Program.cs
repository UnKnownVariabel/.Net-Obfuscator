using System;
using System.Collections.Generic;
using System.Linq;

class Blackjack
{
    static Random rng = new Random();
    static void Main()
    {
        Console.WriteLine("=== Welcome to Blackjack! ===");
        while (true)
        {
            PlayRound();
            Console.Write("\nPlay again? (y/n): ");
            if (Console.ReadLine().ToLower() != "y")
                break;
            Console.Clear();
        }
    }

    static void PlayRound()
    {
        List<string> deck = CreateDeck();
        Shuffle(deck);

        List<string> playerHand = new List<string> { Draw(deck), Draw(deck) };
        List<string> dealerHand = new List<string> { Draw(deck), Draw(deck) };

        Console.WriteLine($"Dealer shows: {dealerHand[0]}");
        Console.WriteLine($"Your hand: {string.Join(", ", playerHand)} (Total: {HandValue(playerHand)})");

        // Player turn
        while (true)
        {
            Console.Write("Hit or stand? (h/s): ");
            string choice = Console.ReadLine().ToLower();
            if (choice == "h")
            {
                playerHand.Add(Draw(deck));
                int total = HandValue(playerHand);
                Console.WriteLine($"You drew: {playerHand.Last()}");
                Console.WriteLine($"Your hand: {string.Join(", ", playerHand)} (Total: {total})");
                if (total > 21)
                {
                    Console.WriteLine("You bust! Dealer wins.");
                    return;
                }
            }
            else if (choice == "s")
            {
                break;
            }
        }

        // Dealer turn
        Console.WriteLine($"\nDealer's hand: {string.Join(", ", dealerHand)} (Total: {HandValue(dealerHand)})");
        while (HandValue(dealerHand) < 17)
        {
            string card = Draw(deck);
            dealerHand.Add(card);
            Console.WriteLine($"Dealer draws: {card} (Total: {HandValue(dealerHand)})");
        }

        int playerTotal = HandValue(playerHand);
        int dealerTotal = HandValue(dealerHand);

        // Results
        if (dealerTotal > 21)
            Console.WriteLine("Dealer busts! You win!");
        else if (dealerTotal > playerTotal)
            Console.WriteLine("Dealer wins!");
        else if (dealerTotal < playerTotal)
            Console.WriteLine("You win!");
        else
            Console.WriteLine("It's a tie!");
    }

    static List<string> CreateDeck()
    {
        string[] suits = { "♠", "♥", "♦", "♣" };
        string[] ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };
        List<string> deck = new List<string>();
        foreach (var suit in suits)
            foreach (var rank in ranks)
                deck.Add(rank + suit);
        return deck;
    }

    static void Shuffle(List<string> deck)
    {
        for (int i = 0; i < deck.Count; i++)
        {
            int j = rng.Next(i, deck.Count);
            var temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }
    }

    static string Draw(List<string> deck)
    {
        string card = deck[0];
        deck.RemoveAt(0);
        return card;
    }

    static int HandValue(List<string> hand)
    {
        int value = 0;
        int aceCount = 0;

        foreach (string card in hand)
        {
            string rank = new string(card.TakeWhile(c => !char.IsSymbol(c)).ToArray());
            if (int.TryParse(rank, out int num))
                value += num;
            else if (rank == "A")
            {
                value += 11;
                aceCount++;
            }
            else
                value += 10;
        }

        // Adjust for aces
        while (value > 21 && aceCount > 0)
        {
            value -= 10;
            aceCount--;
        }

        return value;
    }
}
