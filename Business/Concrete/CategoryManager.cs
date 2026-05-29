using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Resources;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using MapsterMapper;

namespace Business.Concrete
{
    public class CategoryManager(ICategoriesDal categoriesDal) : ICategoryService
    {
        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IResult> AddCategory(Category category)
        {
            await categoriesDal.Add(category);
            return new SuccessResult(Messages.CategoryAddedSuccess);
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IResult> DeleteCategory(Guid id)
        {
            var getCat = await categoriesDal.Get(x => x.Id == id);
            await categoriesDal.Remove(getCat);
            return new SuccessResult(Messages.CategoryDeletedSuccess);
        }

        public async Task<IDataResult<List<Category>>> GetAllCategories()
        {
            var categories = await categoriesDal.GetAll();
            return new SuccessDataResult<List<Category>>(categories);
        }

        public async Task<IDataResult<List<Category>>> GetParentCategories()
        {
            var categories = await categoriesDal.GetAll(x => x.ParentId == null);
            return new SuccessDataResult<List<Category>>(categories, Messages.MainCategoriesRetrieved);
        }

        public async Task<IDataResult<List<Category>>> GetChildCategories(Guid parentId)
        {
            var categories = await categoriesDal.GetAll(x => x.ParentId == parentId);
            return new SuccessDataResult<List<Category>>(categories, Messages.SubCategoriesRetrieved);
        }

        public async Task<IDataResult<List<CategoryHierarchyDto>>> GetCategoryHierarchyAsync()
        {
            var allCategories = await categoriesDal.GetAll();
            var hierarchy = BuildHierarchy(allCategories);
            return new SuccessDataResult<List<CategoryHierarchyDto>>(hierarchy, Messages.CategoriesRetrieved);
        }

        public async Task<IResult> UpdateCategory(Guid id, string name, Guid? parentId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return new ErrorResult(Messages.CategoryNameRequired);

            var entity = await categoriesDal.Get(x => x.Id == id);
            if (entity == null) return new ErrorResult(Messages.CategoryNotFound);

            // Cycle koruması: yeni parent'ı kendi descendant'ları arasında olamaz.
            if (parentId.HasValue)
            {
                if (parentId.Value == id)
                    return new ErrorResult(Messages.CategoryCannotBeOwnParent);

                var allCats = await categoriesDal.GetAll();
                if (IsDescendantOf(allCats, parentId.Value, id))
                    return new ErrorResult(Messages.CategoryCycleDetected);
            }

            entity.Name = name.Trim();
            entity.ParentId = parentId;
            await categoriesDal.Update(entity);
            return new SuccessResult(Messages.CategoryUpdatedSuccess);
        }

        public async Task<IResult> DeleteCategoryAndReparent(Guid id, Guid? reparentTo)
        {
            var entity = await categoriesDal.Get(x => x.Id == id);
            if (entity == null) return new ErrorResult(Messages.CategoryNotFound);

            // reparentTo varsa varlığını ve cycle riskini kontrol et
            if (reparentTo.HasValue)
            {
                if (reparentTo.Value == id)
                    return new ErrorResult(Messages.CategoryCannotBeOwnParent);
                var target = await categoriesDal.Get(x => x.Id == reparentTo.Value);
                if (target == null) return new ErrorResult(Messages.CategoryParentNotFound);
            }

            // Doğrudan child'ları yeniden parent'la.
            var children = await categoriesDal.GetAll(x => x.ParentId == id);
            foreach (var c in children)
            {
                c.ParentId = reparentTo;
                await categoriesDal.Update(c);
            }

            await categoriesDal.Remove(entity);
            return new SuccessResult(Messages.CategoryDeletedSuccess);
        }

        // candidateParent kategorisinin, ataları arasında "ancestorOrSelf" var mı?
        private static bool IsDescendantOf(List<Category> all, Guid candidateParent, Guid ancestorOrSelf)
        {
            var map = all.ToDictionary(c => c.Id, c => c.ParentId);
            var cur = (Guid?)candidateParent;
            int safety = 0;
            while (cur.HasValue && safety++ < 10000)
            {
                if (cur.Value == ancestorOrSelf) return true;
                if (!map.TryGetValue(cur.Value, out var nextParent)) break;
                cur = nextParent;
            }
            return false;
        }

        private List<CategoryHierarchyDto> BuildHierarchy(List<Category> categories)
        {
            var lookup = categories.ToLookup(c => c.ParentId);

            List<CategoryHierarchyDto> BuildLevel(Guid? parentId)
            {
                return lookup[parentId].Select(c => new CategoryHierarchyDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Children = BuildLevel(c.Id)
                }).ToList();
            }

            return BuildLevel(null);
        }
    }
}
