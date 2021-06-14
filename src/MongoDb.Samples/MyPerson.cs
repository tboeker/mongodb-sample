using System;
using System.Collections.Generic;
using Bogus;

namespace MongoDb.Samples
{
    public class MyPerson
    {
        public static List<MyPerson> Generate(int count)
        {
            var testPersons = new Faker<MyPerson>()
                    .CustomInstantiator(f => new MyPerson()
                    {
                        Id = Guid.NewGuid()
                    })
                    .RuleFor(u => u.LastName, f => f.Person.LastName)
                    .RuleFor(u => u.FirstName, f => f.Person.FirstName)
                ;

            return testPersons.Generate(count);
        }

        public Guid Id { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
    }
}