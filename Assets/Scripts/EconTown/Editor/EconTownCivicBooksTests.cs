#if UNITY_EDITOR
using NUnit.Framework;

namespace EconTown
{
    public class EconTownCivicBooksTests
    {
        [Test]
        public void CivicMoney_UsesWanAndYi()
        {
            Assert.AreEqual("¥1.82亿", CivicBooks.Money(18226f));
            Assert.AreEqual("¥600万", CivicBooks.Money(600f));
            Assert.AreEqual("¥1.20亿", CivicBooks.Money(12000f));
            Assert.AreEqual("¥150万", CivicBooks.Money(150f));
            Assert.AreEqual("−¥192万", CivicBooks.Signed(-191.949f));
        }

        [Test]
        public void CivicPeople_UsesQianRen()
        {
            Assert.AreEqual("32万人", CivicBooks.Heads(320f));
            Assert.AreEqual("18.5万人", CivicBooks.Heads(185f));
            Assert.AreEqual("800万 B币", CivicBooks.Foreign(800f));
            Assert.AreEqual("¥570", CivicBooks.MoneyPerPerson(57f));
        }
    }
}
#endif
