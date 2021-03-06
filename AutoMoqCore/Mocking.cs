﻿using System;
using System.Collections.Generic;
using System.Linq;
using AutoMoqCore.Unity;
using Moq;

namespace AutoMoqCore
{
    // ReSharper disable once InconsistentNaming
    public interface IMocking
    {
        bool AMockHasNotBeenRegisteredFor(Type type);
        void RegisterThisMock(object mock, Type type);
        object GetTheRegisteredMockFor(Type type);
        MockCreationResult CreateAMockObjectFor(Type type, MockBehavior mockBehavior);
        MockCreationResult CreateANewMockObjectAndRegisterIt(Type type);
        void SetMock(Type type, object mock);
        void SetInstance<T>(T instance) where T : class;
        Mock<T> GetMockByCreatingAMockIfOneHasNotAlreadyBeenCreated<T>() where T : class;
        Mock<T> GetMockByCreatingAMockIfOneHasNotAlreadyBeenCreated<T>(MockBehavior mockBehavior) where T : class;
        void VerifyAll();
        void Verify();
    }

    public class MockingWithMoq : IMocking
    {
        private readonly IIoC _ioc;
        private readonly MockRepository _mockRepository;

        public MockingWithMoq(Config config, IIoC ioc)
        {
            this._ioc = ioc;
            RegisteredMocks = new Dictionary<Type, object>();
            _mockRepository = new MockRepository(config.MockBehavior);
        }

        public IDictionary<Type, object> RegisteredMocks { get; }

        public bool AMockHasNotBeenRegisteredFor(Type type)
        {
            return RegisteredMocks.ContainsKey(type) == false;
        }

        public void RegisterThisMock(object mock, Type type)
        {
            RegisteredMocks.Add(type, mock);
        }

        public object GetTheRegisteredMockFor(Type type)
        {
            return RegisteredMocks.First(x => x.Key == type).Value;
        }

        public MockCreationResult CreateAMockObjectFor(Type type, MockBehavior mockBehavior = MockBehavior.Default)
        {
            var createMethod = _mockRepository.GetType()
                .GetMethod("Create", new[] {typeof (object[])}).MakeGenericMethod(type);

            var parameters = new List<object>();
            if (mockBehavior != MockBehavior.Default) parameters.Add(mockBehavior);
            var mock = (Mock) createMethod.Invoke(_mockRepository, new object[] {parameters.ToArray()});

            return new MockCreationResult
            {
                ActualObject = mock.Object,
                MockObject = mock
            };
        }

        public MockCreationResult CreateANewMockObjectAndRegisterIt(Type type)
        {
            var result = CreateAMockObjectFor(type);

            var mock = (Mock) result.MockObject;

            RegisterThisObjectInTheIoCContainer(type, mock);
            RegisterThisMockWithAutoMoq(type, mock);

            return result;
        }

        private void RegisterThisMockWithAutoMoq(Type type, Mock mock)
        {
            _ioc.Resolve<AutoMoqer>().SetMock(type, mock);
        }

        private void RegisterThisObjectInTheIoCContainer(Type type, Mock mock)
        {
            // this is meant to replicate this generic method call
            // container.RegisterInstance<T>(mock.Object)
            _ioc.GetType()
                .GetMethods()
                .First(x => x.Name == "RegisterInstance" && x.IsGenericMethod)
                .MakeGenericMethod(type)
                .Invoke(_ioc, new[] {mock.Object});
        }

        public MockCreationResult CreateANewMockObjectAndRegisterIt<T>(MockBehavior mockBehavior = MockBehavior.Default) where T : class
        {
            var result = CreateAMockObjectFor(typeof (T), mockBehavior);
            var mock = (Mock<T>) result.MockObject;
            _ioc.RegisterInstance(mock.Object);
            _ioc.Resolve<AutoMoqer>().SetMock(typeof (T), mock);
            return result;
        }

        public void SetMock(Type type, object mock)
        {
            if (AMockHasNotBeenRegisteredFor(type) == false) return;
            RegisterThisMock(mock, type);
        }

        public void SetInstance<T>(T instance) where T : class
        {
            _ioc.RegisterInstance(instance);
            SetMock(typeof (T), null);
        }

        public Mock<T> GetMockByCreatingAMockIfOneHasNotAlreadyBeenCreated<T>() where T : class
        {
            return GetMockByCreatingAMockIfOneHasNotAlreadyBeenCreated<T>(MockBehavior.Default);
        }

        public Mock<T> GetMockByCreatingAMockIfOneHasNotAlreadyBeenCreated<T>(MockBehavior mockBehavior) where T : class
        {
            var type = typeof (T);
            if (GetMockHasNotBeenCalledForThisType(type))
                CreateANewMockAndRegisterIt<T>(mockBehavior);

            return TheRegisteredMockForThisType<T>(type);
        }

        private Mock<T> TheRegisteredMockForThisType<T>(Type type) where T : class
        {
            return (Mock<T>) GetTheRegisteredMockFor(type);
        }

        private void CreateANewMockAndRegisterIt<T>(MockBehavior mockBehavior) where T : class
        {
            CreateANewMockObjectAndRegisterIt<T>(mockBehavior);
        }

        private bool GetMockHasNotBeenCalledForThisType(Type type)
        {
            return AMockHasNotBeenRegisteredFor(type);
        }

        public void VerifyAll()
        {
            _mockRepository.VerifyAll();
        }

        public void Verify()
        {
            _mockRepository.Verify();
        }
    }
}