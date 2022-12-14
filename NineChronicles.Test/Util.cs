namespace NineChronicles.Test;

public class Util
{
    public static int Select<T>(IEnumerable<T> list)
    {
        bool usable = false;
        int index = -1;
        while (!usable)
        {
            var selectedIndex = Console.ReadLine();
            usable = int.TryParse(selectedIndex, out index);
            if (!usable)
            {
                Console.WriteLine("Please input number.");
            }
            else if (index > list.ToList().Count)
            {
                Console.WriteLine($"{index} is not on the list. Please set right one: ");
                usable = false;
            }
        }

        return index;
    }
}
