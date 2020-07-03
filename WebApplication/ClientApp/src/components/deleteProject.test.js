import React from 'react';
import Enzyme, { shallow } from 'enzyme';
import Adapter from 'enzyme-adapter-react-16';
import { DeleteProject } from './deleteProject';

Enzyme.configure({ adapter: new Adapter() });

const projIdA = 'project A';
const projIdB = 'project B';

const props = {
    deleteProjectDlgVisible: true,
    checkedProjects: [ projIdA , projIdB ]
};

const showDeleteProjectMockFn = jest.fn();
const deleteProjectMockFn = jest.fn();

describe('Delete project confirmation dialog', () => {

  beforeEach(() => {
    showDeleteProjectMockFn.mockClear();
    deleteProjectMockFn.mockClear();
  });

  it('Lists the projects', () => {
    const wrapper = shallow(<DeleteProject { ...props } showDeleteProject={showDeleteProjectMockFn} deleteProject={deleteProjectMockFn} />);
    const list = wrapper.find("#deleteProjectModal > div.deleteProjectListContainer > ul");
    const items = list.find('li');
    expect(items.at(0).text()).toEqual(projIdA);
    expect(items.at(1).text()).toEqual(projIdB);
  });

  it('Delete button calls the delete handler', () => {
    const wrapper = shallow(<DeleteProject { ...props } showDeleteProject={showDeleteProjectMockFn} deleteProject={deleteProjectMockFn} />);
    const button = wrapper.find("#delete_ok_button");
    button.simulate('click');
    expect(deleteProjectMockFn).toHaveBeenCalled();
  });


  it('Cancel button closes the dialog', () => {
    const wrapper = shallow(<DeleteProject { ...props } showDeleteProject={showDeleteProjectMockFn} deleteProject={deleteProjectMockFn} />);
    const button = wrapper.find("#cancel_button");
    button.simulate('click');
    expect(showDeleteProjectMockFn).toHaveBeenCalledWith(false);
    expect(deleteProjectMockFn).toHaveBeenCalledTimes(0);
  });
});