﻿//-----------------------------------------------------------------------
// <copyright file="AbstractGetSet.cs" company="Marimer LLC">
//     Copyright (c) Marimer LLC. All rights reserved.
//     Website: http://www.lhotka.net/cslanet/
// </copyright>
// <summary>no summary</summary>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Csla.Test.Silverlight.PropertyGetSet
{
  [Serializable]
  public class AbstractGetSet<T> : BusinessBase<T> where T : AbstractGetSet<T>
  {
    protected static PropertyInfo<int> IdProperty = RegisterProperty<int>(new PropertyInfo<int>("Id", "Id"));
    public int Id
    {
      get { return GetProperty<int>(IdProperty); }
      set { SetProperty<int>(IdProperty, value); }
    }

    private static int _forceLoad;

    protected AbstractGetSet()
    {
      _forceLoad = 0;
    }

    protected override void OnDeserialized(System.Runtime.Serialization.StreamingContext context)
    {
      _forceLoad = 0;
      base.OnDeserialized(context);
    }
  }
}